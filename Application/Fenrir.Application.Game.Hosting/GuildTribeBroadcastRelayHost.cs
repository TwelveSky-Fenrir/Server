using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard fan-out, Game-side: the composition root and drain/poll loop for
///     GuildAnnouncement/GuildChat/TribeAnnouncement/TribeAnnouncementScroll's cluster-wide delivery --
///     Fenrir's SQL-mediated stand-in for legacy's <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> relay uplink (see
///     <see cref="GameServerOptions.GuildTribeBroadcastPollIntervalSeconds" />'s own remarks for the full
///     citation). Same "one instance, three registrations" composition shape as <c>EventLogFlushHost</c>:
///     <c>*.Services</c> producers (<c>GuildAnnouncementService</c>/<c>GuildChatService</c>/
///     <c>TribeAnnouncementService</c>/<c>TribeAnnouncementScrollService</c>) consume
///     <see cref="IGuildTribeBroadcastRelayQueue" /> only, immediately after each one's own unchanged,
///     synchronous same-shard delivery via <see cref="ZoneRegistry" />.
/// </summary>
/// <remarks>
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and
///         publishes each entry via <see cref="IGuildTribeBroadcastRelayRepository.PublishAsync" /> so every
///         OTHER live shard's own next poll picks it up; (2) calls
///         <see cref="IGuildTribeBroadcastRelayRepository.PollAsync" /> for rows some OTHER shard published
///         since this shard's own last poll and delivers each to this shard's own locally-hosted matching
///         players. A shard never re-delivers its own published rows to itself -- the originating
///         <c>*.Services</c> call site already delivered synchronously to this shard's local
///         <see cref="ZoneRegistry" /> before ever enqueuing here, and the poll's own SQL predicate excludes
///         <c>SourceShardId = @ShardId</c>.
///     </para>
///     <para>
///         Delivery filtering deliberately matches each opcode's own local (same-shard) delivery loop exactly:
///         no <see cref="PlayerRuntimeState.IsMovingZone" /> guard, since the legacy contract's own Cluster
///         Overview states the guild/tribe relay delivery cases (110-115) check only session-ready state, not
///         the mid-transfer flag (unlike <c>GlobalAnnouncementService</c>'s relay case 102). Every player
///         tracked in a <see cref="Zone" />'s own <see cref="Zone.Players" /> is already "ready" by
///         construction (<c>HandleEnter</c> is the only add site), so no separate readiness check is needed
///         either.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both the publish and delivery loops -- one failed publish or one
///         malformed inbound row must never block the rest of the batch or take the poll cycle down, the same
///         "isolate per-entity failures" posture <c>Zone.DrainInbox</c>/<c>Zone.Simulate</c> already apply to
///         the tick loop.
///     </para>
/// </remarks>
public sealed class GuildTribeBroadcastRelayHost(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildTribeBroadcastRelayHost> logger) : BackgroundService, IGuildTribeBroadcastRelayQueue
{
    // Small, bursty at most (guild/tribe chat is human-typing-speed, not a per-tick/per-combat volume) --
    // generous headroom over any plausible per-cycle burst without EventLogQueue's own larger 4096/256
    // batch-oriented sizing (that queue's own remarks explain why it needs to absorb per-tick combat-roll
    // volume; this one does not).
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    private readonly Channel<GuildTribeBroadcastRelayEntry> _outbox = Channel.CreateBounded<GuildTribeBroadcastRelayEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            // Wait mode's TryWrite returns false immediately when full (only WriteAsync actually awaits
            // space) -- the honest, non-blocking backpressure signal Enqueue needs, since it only ever calls
            // TryWrite. Same reasoning as EventLogQueue's own choice of Wait over DropWrite.
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public bool Enqueue(GuildTribeBroadcastRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard guild/tribe broadcast relay outbox full on shard {ShardId}; dropping one {Kind} " +
            "broadcast (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.Kind);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.GuildTribeBroadcastPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed cycle just delays cross-shard delivery by one more poll interval -- never worth
                // crashing the GameServer over (same-shard delivery for every one of these four opcodes is
                // entirely unaffected).
                logger.LogError(ex, "Guild/tribe broadcast relay poll failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
    public async ValueTask PollOnceAsync(CancellationToken ct)
    {
        await FlushOutboundAsync(ct).ConfigureAwait(false);
        await DeliverInboundAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask FlushOutboundAsync(CancellationToken ct)
    {
        var reader = _outbox.Reader;
        var drained = 0;

        while (drained < MaxDrainedPerCycle && reader.TryRead(out var entry))
        {
            drained++;
            try
            {
                await relay.PublishAsync(entry, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Dropped, not requeued -- same "no retry, accepted residual gap" posture as EventLogQueue's
                // own failed-batch handling: this shard's OWN players already saw this broadcast synchronously,
                // only the other-shard fan-out for this one message is lost.
                logger.LogError(ex,
                    "Failed to publish a {Kind} broadcast to the cross-shard relay from shard {ShardId}; " +
                    "cross-shard fan-out for this one message is lost (same-shard delivery already happened)",
                    entry.Kind, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.GuildTribeBroadcastRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                DeliverLocally(dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed broadcast {RelayId} (kind {Kind}) on shard {ShardId}",
                    dto.RelayId, dto.Kind, shardId);
            }
    }

    private void DeliverLocally(GuildTribeBroadcastRelayDto dto)
    {
        switch ((GuildTribeBroadcastKind)dto.Kind)
        {
            case GuildTribeBroadcastKind.GuildAnnouncement:
                DeliverToGuild(dto.GuildId,
                    new GuildAnnouncementResponse { AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.GuildChat:
                var link = new ItemLinkInfo
                {
                    Index = dto.ItemLinkIndex ?? 0,
                    Activity = dto.ItemLinkActivity ?? 0,
                    Value = dto.ItemLinkValue ?? 0,
                    Socket = [dto.ItemLinkSocket0 ?? 0, dto.ItemLinkSocket1 ?? 0, dto.ItemLinkSocket2 ?? 0]
                };
                DeliverToGuild(dto.GuildId,
                    new GuildChatResponse { AvatarName = dto.AvatarName, Content = dto.Content, Link = link });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncement:
                DeliverToTribe(dto.Tribe, new TribeAnnouncementResponse
                    { TribeRole = dto.RoleField, AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            case GuildTribeBroadcastKind.TribeAnnouncementScroll:
                // TribeRole here actually carries the sender's tribe number, not a role -- see
                // TribeAnnouncementScrollResponse's own docstring for the wire-field quirk this mirrors.
                DeliverToTribe(dto.Tribe, new TribeAnnouncementScrollResponse
                    { TribeRole = dto.RoleField, AvatarName = dto.AvatarName, Content = dto.Content });
                break;

            default:
                logger.LogWarning("Relayed broadcast {RelayId} has unrecognized Kind {Kind}; dropped",
                    dto.RelayId, dto.Kind);
                break;
        }
    }

    private void DeliverToGuild(int? guildId, GuildAnnouncementResponse response)
    {
        if (guildId is not { } id)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.GuildId == id)
                recipient.Session.Send(response);
    }

    private void DeliverToGuild(int? guildId, GuildChatResponse response)
    {
        if (guildId is not { } id)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.GuildId == id)
                recipient.Session.Send(response);
    }

    private void DeliverToTribe(byte? tribe, TribeAnnouncementResponse response)
    {
        if (tribe is not { } t)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.Tribe == t)
                recipient.Session.Send(response);
    }

    private void DeliverToTribe(byte? tribe, TribeAnnouncementScrollResponse response)
    {
        if (tribe is not { } t)
            return;

        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
            if (recipient.Tribe == t)
                recipient.Session.Send(response);
    }
}
