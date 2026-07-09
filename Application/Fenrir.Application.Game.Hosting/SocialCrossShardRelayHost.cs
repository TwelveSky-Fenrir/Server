using System.Collections.Frozen;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard POINT-TO-POINT relay, Game-side: the composition root and drain/poll loop for the six
///     character-to-character negotiation flows (Party invite, Friend ask, Mentor ask, Duel ask, Trade
///     invite, Guild invite) once a same-shard <c>ZoneRegistry</c> scan misses and
///     <c>ICharacterShardLocationRepository.FindByNameAsync</c> resolves the target on a different live
///     shard. Point-to-point sibling of <see cref="GuildTribeBroadcastRelayHost" /> -- see that class's own
///     remarks for the shared "SQL-mediated relay, no <c>ts25center</c>-equivalent process" rationale this
///     one shares; the one deliberate divergence is <see cref="ISocialCrossShardRelayRepository.PollAsync" />
///     filtering by <c>TargetShardId = @ShardId</c> (point-to-point, every row already names its one
///     intended recipient shard) rather than excluding <c>SourceShardId = @ShardId</c> (fan-out).
/// </summary>
/// <remarks>
///     <para>
///         Same "one instance, three registrations" composition shape as <see cref="GuildTribeBroadcastRelayHost" />:
///         the six negotiation *.Services producers consume <see cref="ISocialCrossShardRelayQueue" /> only,
///         to publish an Ask (immediately after their own local pre-checks that do not need the target's
///         live state -- asker busy/negotiating gate, same-tribe check against the resolved row's
///         denormalized Tribe, map-based inter-tribe carve-out) or an Answer (after a human responds to a
///         cross-shard prompt, addressed back to the Ask's own
///         <see cref="SocialCrossShardRelayEntry.SourceShardId" />/<see cref="SocialCrossShardRelayEntry.SourceCharacterId" />
///         via <see cref="SocialCrossShardRelayEntry.AskRelayId" />).
///     </para>
///     <para>
///         UNLIKE <see cref="GuildTribeBroadcastRelayHost" />, this host does NOT deliver a row itself --
///         local delivery needs each negotiation's own registry state (busy/stunned/dead checks, Mentor's
///         level gate, the in-memory pending-entry table keyed by RelayId/AskRelayId), which belongs in each
///         of the six *.Services classes, not in this generic poll loop. Every delivered row is instead
///         routed to whichever registered <see cref="ISocialCrossShardRelayHandler" /> owns the row's own
///         <see cref="SocialCrossShardRelayDto.Kind" />, split by
///         <see cref="SocialCrossShardRelayDto.MessageType" /> into
///         <see cref="ISocialCrossShardRelayHandler.HandleAskAsync" />/
///         <see cref="ISocialCrossShardRelayHandler.HandleAnswerAsync" />. Until each *.Services registry
///         implements and registers a handler for its own <see cref="SocialCrossShardRelayKind" />, a
///         delivered row for that Kind is logged and dropped -- same fail-safe posture as
///         <see cref="GuildTribeBroadcastRelayHost" />'s own unrecognized-<c>Kind</c> default case, and it
///         costs nothing else: same-shard delivery for every one of these six flows is entirely unaffected
///         either way.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both the publish and delivery loops -- one failed publish or one
///         malformed/unhandled inbound row must never block the rest of the batch or take the poll cycle
///         down, the same "isolate per-entity failures" posture <c>Zone.DrainInbox</c>/<c>Zone.Simulate</c>
///         already apply to the tick loop.
///     </para>
/// </remarks>
public sealed class SocialCrossShardRelayHost(
    IEnumerable<ISocialCrossShardRelayHandler> handlers,
    ISocialCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<SocialCrossShardRelayHost> logger) : BackgroundService, ISocialCrossShardRelayQueue
{
    // Six negotiation kinds, human click/typing-speed traffic (party/duel/trade/mentor/friend/guild
    // invites), not a per-tick/per-combat volume -- same sizing rationale as GuildTribeBroadcastRelayHost's
    // own outbox.
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    // Built once from whatever *.Services registries are registered as ISocialCrossShardRelayHandler at
    // DI-composition time (none yet -- the registries are a follow-up piece of this same design). Safe to
    // enumerate the DI collection eagerly in the primary constructor body: by the time anything resolves
    // SocialCrossShardRelayHost, every ISocialCrossShardRelayHandler registration the container knows about
    // already exists.
    private readonly FrozenDictionary<SocialCrossShardRelayKind, ISocialCrossShardRelayHandler> _handlersByKind =
        handlers.ToFrozenDictionary(static h => h.Kind);

    private readonly Channel<SocialCrossShardRelayEntry> _outbox =
        Channel.CreateBounded<SocialCrossShardRelayEntry>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                // Wait mode's TryWrite returns false immediately when full (only WriteAsync actually awaits
                // space) -- the honest, non-blocking backpressure signal Enqueue needs, since it only ever
                // calls TryWrite. Same reasoning as GuildTribeBroadcastRelayHost's own choice of Wait over
                // DropWrite.
                FullMode = BoundedChannelFullMode.Wait
            });

    /// <inheritdoc />
    public bool Enqueue(SocialCrossShardRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard social relay outbox full on shard {ShardId}; dropping one {Kind}/{MessageType} row " +
            "addressed to character {TargetCharacterId} on shard {TargetShardId} (any same-shard delivery " +
            "already happened, only the cross-shard leg of this one step is lost)",
            options.Value.ShardId, entry.Kind, entry.MessageType, entry.TargetCharacterId, entry.TargetShardId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SocialCrossShardRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed cycle just delays cross-shard delivery by one more poll interval -- never worth
                // crashing the GameServer over (same-shard delivery for every one of these six flows is
                // entirely unaffected).
                logger.LogError(ex, "Social cross-shard relay poll failed for shard {ShardId}",
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
                // GuildTribeBroadcastRelayHost's own failed-publish handling.
                logger.LogError(ex,
                    "Failed to publish a {Kind}/{MessageType} row to the cross-shard social relay from shard " +
                    "{ShardId} (target character {TargetCharacterId} on shard {TargetShardId}); this one step " +
                    "is lost",
                    entry.Kind, entry.MessageType, options.Value.ShardId, entry.TargetCharacterId,
                    entry.TargetShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.SocialCrossShardRelayRetentionSeconds;

        var incoming = await relay.PollAsync(shardId, retentionSeconds, ct).ConfigureAwait(false);
        if (incoming.IsEmpty)
            return;

        foreach (var dto in incoming)
            try
            {
                await DeliverLocallyAsync(dto, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Failed to locally deliver relayed social row {RelayId} (kind {Kind}, message {MessageType}) " +
                    "on shard {ShardId}",
                    dto.RelayId, dto.Kind, dto.MessageType, shardId);
            }
    }

    private async ValueTask DeliverLocallyAsync(SocialCrossShardRelayDto dto, CancellationToken ct)
    {
        if (!_handlersByKind.TryGetValue((SocialCrossShardRelayKind)dto.Kind, out var handler))
        {
            logger.LogWarning(
                "Relayed social row {RelayId} has Kind {Kind} with no registered ISocialCrossShardRelayHandler; dropped",
                dto.RelayId, dto.Kind);
            return;
        }

        switch ((SocialCrossShardRelayMessageType)dto.MessageType)
        {
            case SocialCrossShardRelayMessageType.Ask:
                await handler.HandleAskAsync(dto, ct).ConfigureAwait(false);
                break;

            case SocialCrossShardRelayMessageType.Answer:
                await handler.HandleAnswerAsync(dto, ct).ConfigureAwait(false);
                break;

            default:
                logger.LogWarning("Relayed social row {RelayId} has unrecognized MessageType {MessageType}; dropped",
                    dto.RelayId, dto.MessageType);
                break;
        }
    }
}
