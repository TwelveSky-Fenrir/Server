using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

/// <summary>
///     Cross-shard fan-out, Game-side, for the guild-buff-reserve-exhaustion immediate strip-effect push --
///     Fenrir's SQL-mediated stand-in for legacy's ts25center-driven <c>BroadcastZone()</c> cluster-wide send
///     (see <see cref="GameServerOptions.GuildBuffExpiryRelayPollIntervalSeconds" />'s own remarks for the
///     full citation). Same "one instance, two registrations" composition shape as
///     <c>GuildTribeBroadcastRelayHost</c>: <see cref="GuildBuffDecayHost" /> is the sole producer, consuming
///     <see cref="IGuildBuffExpiryRelayQueue" /> only, immediately after its own same-shard delivery
///     (<see cref="Zone.PostGuildBuffExpiryCommand" /> posted directly onto every zone this shard hosts).
/// </summary>
/// <remarks>
///     Unlike <c>GuildTribeBroadcastRelayHost</c>'s own inbound delivery (a stateless <c>Session.Send</c>,
///     safe from any thread), this push MUTATES <see cref="PlayerRuntimeState" /> fields
///     (<see cref="PlayerRuntimeState.GuildBuffActive" />/<see cref="PlayerRuntimeState.GuildBuffActiveMirror" />),
///     so inbound delivery here posts a <see cref="GuildBuffExpiryZoneCommand" /> onto each of this shard's own
///     hosted zones instead of touching a <see cref="PlayerRuntimeState" /> directly -- respecting every
///     <see cref="Zone" />'s single-writer-per-tick-thread invariant.
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and
///         publishes each entry via <see cref="IGuildBuffExpiryRelayRepository.PublishAsync" /> so every OTHER
///         live shard's own next poll picks it up; (2) calls
///         <see cref="IGuildBuffExpiryRelayRepository.PollAsync" /> for rows some OTHER shard published since
///         this shard's own last poll and posts a <see cref="GuildBuffExpiryZoneCommand" /> to each of this
///         shard's own hosted zones. A shard never re-delivers its own published rows to itself --
///         <see cref="GuildBuffDecayHost" /> already delivered synchronously to this shard's own hosted zones
///         before ever enqueuing here, and the poll's own SQL predicate excludes
///         <c>SourceShardId = @ShardId</c>.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both the publish and delivery loops, same "isolate per-entity
///         failures" posture as every sibling relay host in this cluster.
///     </para>
/// </remarks>
public sealed class GuildBuffExpiryRelayHost(
    ZoneRegistry zones,
    IGuildBuffExpiryRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildBuffExpiryRelayHost> logger) : BackgroundService, IGuildBuffExpiryRelayQueue
{
    // Small, rare (at most one push per guild per exhaustion, gated by GuildBuffDecayHost's own 30 s poll) --
    // generous headroom over any plausible per-cycle burst without GuildTribeBroadcastRelayHost's own larger
    // 1024/512 chat-volume sizing.
    private const int QueueCapacity = 64;
    private const int MaxDrainedPerCycle = 32;

    private readonly Channel<GuildBuffExpiryRelayEntry> _outbox = Channel.CreateBounded<GuildBuffExpiryRelayEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            // Wait mode's TryWrite returns false immediately when full -- the honest, non-blocking
            // backpressure signal Enqueue needs, since it only ever calls TryWrite. Same reasoning as
            // GuildTribeBroadcastRelayHost's own choice of Wait over DropWrite.
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public bool Enqueue(GuildBuffExpiryRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard guild-buff-expiry relay outbox full on shard {ShardId}; dropping the fan-out for " +
            "guild {GuildId} (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.GuildId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.GuildBuffExpiryRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed cycle just delays cross-shard delivery by one more poll interval -- never worth
                // crashing the GameServer over (same-shard delivery is entirely unaffected).
                logger.LogError(ex, "Guild-buff-expiry relay poll failed for shard {ShardId}", options.Value.ShardId);
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
                // Dropped, not requeued -- same "no retry, accepted residual gap" posture as every sibling
                // relay host: this shard's OWN matching players already saw the flip synchronously, only the
                // other-shard fan-out for this one push is lost.
                logger.LogError(ex,
                    "Failed to publish a guild-buff-expiry push for guild {GuildId} to the cross-shard relay " +
                    "from shard {ShardId}; cross-shard fan-out for this one push is lost (same-shard delivery " +
                    "already happened)", entry.GuildId, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.GuildBuffExpiryRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                var command = new GuildBuffExpiryZoneCommand(dto.GuildId, dto.NewBuffTime);
                foreach (var zone in zones.Zones)
                    zone.PostGuildBuffExpiryCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed guild-buff-expiry push {RelayId} (guild {GuildId}) on shard {ShardId}",
                    dto.RelayId, dto.GuildId, shardId);
            }
    }
}
