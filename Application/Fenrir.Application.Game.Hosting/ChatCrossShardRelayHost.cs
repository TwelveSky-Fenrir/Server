using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard whisper (op39 / <c>SECRET_CHAT</c>) delivery, Game-side: the composition root and drain/poll
///     loop for the ONE point-to-point private-chat opcode -- Fenrir's SQL-mediated stand-in for legacy's own
///     <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> whisper relay (see
///     <see cref="GameServerOptions.ChatCrossShardRelayPollIntervalSeconds" />'s own remarks for the full
///     citation trail). Point-to-point sibling of <see cref="SocialCrossShardRelayHost" />, minus its Ask/Answer
///     negotiation -- a whisper is fire-and-forget. <c>WhisperService.ResolveAsync</c> is the sole producer: it
///     consumes <see cref="IChatCrossShardRelayQueue" /> only, publishing one row after a same-shard
///     <see cref="ZoneRegistry" /> miss resolves the target on another live shard.
/// </summary>
/// <remarks>
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and publishes
///         each entry via <see cref="IChatCrossShardRelayRepository.PublishAsync" /> so the ONE shard named in
///         each row's <c>TargetShardId</c> picks it up on its own next poll; (2) calls
///         <see cref="IChatCrossShardRelayRepository.PollAsync" /> for rows addressed to THIS shard and delivers
///         each to the one locally-hosted matching player.
///     </para>
///     <para>
///         UNLIKE <see cref="SocialCrossShardRelayHost" />, this host delivers a row itself rather than routing
///         to a per-Kind handler: whisper delivery needs no per-negotiation registry state (no busy/level gate,
///         no in-memory pending-entry table) -- it is a single fire-and-forget send to the target if present,
///         exactly the shape <see cref="GuildTribeBroadcastRelayHost" /> uses for its own fan-out delivery.
///     </para>
///     <para>
///         Clean-drop parity is the load-bearing delivery behavior (op39 contract, delivery/drop side): a row is
///         delivered only to a locally-present target that is NOT mid zone-transfer. A target that has since
///         left this shard, or is flagged <see cref="PlayerRuntimeState.IsMovingZone" />, is silently dropped --
///         no error, no retry, no notification back to the sender, who already received the "accepted, target
///         located" acknowledgement (Result=0) at lookup time. Delivery matches on <c>TargetCharacterId</c>
///         (resolved up-front from the cross-shard directory), a more precise match than legacy's own
///         per-zone name scan.
///     </para>
///     <para>
///         The optional item-link attachment the legacy whisper can carry is NOT relayed cross-shard (see
///         <see cref="ChatCrossShardWhisperEntry" />/<c>runtime.ChatCrossShardRelay.sql</c>) -- the delivered
///         <see cref="WhisperResponse" /> carries a zeroed link. The same-shard whisper path is unaffected.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both loops -- one failed publish or one undeliverable inbound row must
///         never block the rest of the batch or take the poll cycle down, the same "isolate per-entity failures"
///         posture the tick loop applies.
///     </para>
/// </remarks>
public sealed class ChatCrossShardRelayHost(
    ZoneRegistry zones,
    IChatCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ChatCrossShardRelayHost> logger) : BackgroundService, IChatCrossShardRelayQueue
{
    // Whisper is human-typing-speed traffic, not a per-tick/per-combat volume -- same sizing rationale and
    // values as SocialCrossShardRelayHost's own outbox.
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    // Socket is a reference-type array; a bare `default` ItemLinkInfo would leave it null and crash the wire
    // writer. Same zeroed-link constant WhisperHandler uses for its non-item-link responses.
    private static readonly ItemLinkInfo EmptyLink =
        new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    private readonly Channel<ChatCrossShardWhisperEntry> _outbox =
        Channel.CreateBounded<ChatCrossShardWhisperEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                // Wait mode's TryWrite returns false immediately when full (only WriteAsync actually awaits
                // space) -- the honest, non-blocking backpressure signal Enqueue needs, since it only ever
                // calls TryWrite. Same reasoning as SocialCrossShardRelayHost's own choice of Wait over
                // DropWrite.
                FullMode = BoundedChannelFullMode.Wait
            });

    /// <inheritdoc />
    public bool Enqueue(ChatCrossShardWhisperEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard whisper relay outbox full on shard {ShardId}; dropping one whisper from {SourceName} " +
            "to {TargetName} (character {TargetCharacterId} on shard {TargetShardId}) -- the sender already saw " +
            "the accepted acknowledgement, only this one cross-shard delivery is lost",
            options.Value.ShardId, entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId,
            entry.TargetShardId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ChatCrossShardRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed cycle just delays cross-shard whisper delivery by one more poll interval -- never
                // worth crashing the GameServer over (same-shard whisper delivery is entirely unaffected).
                logger.LogError(ex, "Cross-shard whisper relay poll failed for shard {ShardId}",
                    options.Value.ShardId);
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
                // Dropped, not requeued -- same "no retry, accepted residual gap" posture as
                // SocialCrossShardRelayHost's own failed-publish handling. The sender already saw the accepted
                // acknowledgement; only this one cross-shard whisper is lost.
                logger.LogError(ex,
                    "Failed to publish a cross-shard whisper from {SourceName} to {TargetName} (character " +
                    "{TargetCharacterId} on shard {TargetShardId}) from shard {ShardId}; this one whisper is lost",
                    entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId, entry.TargetShardId,
                    options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.ChatCrossShardRelayRetentionSeconds;

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
                    "Failed to locally deliver relayed whisper {RelayId} (from {SourceName} to {TargetName}) on " +
                    "shard {ShardId}",
                    dto.RelayId, dto.SourceAvatarName, dto.TargetAvatarName, shardId);
            }
    }

    private void DeliverLocally(ChatCrossShardWhisperDto dto)
    {
        // Clean-drop parity: the target may have logged off or hopped shards between the sender's up-front
        // directory lookup and this poll landing. A miss here is silently discarded -- no error, no retry, no
        // notification back to the sender (who already received Result=0). The target-still-mid-transfer case is
        // covered by the IsMovingZone guard below; TryGetPlayer additionally already returns false for a player
        // that is mid-handoff (present in no zone's live player map during the actual transfer).
        if (!zones.TryGetPlayer(dto.TargetCharacterId, out var target) || target.IsMovingZone)
        {
            logger.LogDebug(
                "Relayed whisper {RelayId} from {SourceName} to character {TargetCharacterId} on shard {ShardId} " +
                "found no deliverable local target (departed or mid zone-transfer); dropped",
                dto.RelayId, dto.SourceAvatarName, dto.TargetCharacterId, options.Value.ShardId);
            return;
        }

        // Result=3 delivery to the recipient: carries the SENDER's name, the content, and the sender's auth flag
        // (forwarded verbatim so a GM whisper still renders elevated). The item link is not relayed cross-shard
        // (see this host's own remarks) -- delivered zeroed.
        target.Session.Send(new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = dto.SourceAvatarName,
            Content = dto.Content,
            AuthType = dto.SenderAuthType,
            Link = EmptyLink
        });
    }
}
