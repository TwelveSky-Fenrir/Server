using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard fan-out, Game-side: the composition root and drain/poll loop for the party-membership
///     resync-on-reconnect flow (legacy RELAY_LOGIN_PARTY_CHECK / hub case 108 -- Server/Header/Protocol/
///     STRUCT.h:1093-1098, Server/ts25center/S04_MyWork02.cpp:1443-1494). Fenrir's SQL-mediated stand-in for
///     that <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> leg, since Fenrir has no <c>ts25center</c>-equivalent
///     process and its <c>PartyRegistry</c> is per-shard in-memory only (the documented "cross-shard scope
///     gap"). Same "one instance, three registrations" composition shape as
///     <see cref="SocialCrossShardRelayHost" />.
/// </summary>
/// <remarks>
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and
///         publishes each entry via <see cref="IPartyResyncRelayRepository.PublishAsync" /> so every OTHER
///         live shard's own next poll picks it up (fan-out -- the legacy hub broadcasts its reconciliation
///         RESULT to every connected zone, not a point reply); (2) calls
///         <see cref="IPartyResyncRelayRepository.PollAsync" /> for rows some OTHER shard published since this
///         shard's own last poll and routes each to the registered <see cref="IPartyResyncRelayHandler" />.
///     </para>
///     <para>
///         UNLIKE its fan-out sibling <see cref="GuildTribeBroadcastRelayHost" />, this host does NOT deliver a
///         row itself -- party reconciliation needs this shard's own live <c>PartyRegistry</c> (which member is
///         where, which party is still alive), which belongs in a <c>*.Services</c> handler, not this generic
///         poll loop. That is the same "route to a handler, do not deliver generically" split
///         <see cref="SocialCrossShardRelayHost" /> uses. The party-authority <c>*.Services</c> registry
///         (<c>PartyResyncRelayHandler</c>) registers itself as <see cref="IPartyResyncRelayHandler" />; if a
///         given composition has none registered (e.g. a narrower test host), a delivered row is logged and
///         dropped instead -- same fail-safe posture as <see cref="SocialCrossShardRelayHost" />'s own
///         no-registered-handler case, and it costs nothing: same-shard party membership is entirely
///         unaffected either way.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both loops -- one failed publish or one unhandled inbound row must
///         never block the rest of the batch or take the poll cycle down, the same "isolate per-entity
///         failures" posture the tick loop applies.
///     </para>
/// </remarks>
public sealed class PartyResyncRelayHost(
    IEnumerable<IPartyResyncRelayHandler> handlers,
    IPartyResyncRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHost> logger) : BackgroundService, IPartyResyncRelayQueue
{
    // Party resync is a per-reconnect event (human login cadence), not a per-tick/per-combat volume -- same
    // sizing rationale and values as SocialCrossShardRelayHost's own outbox.
    private const int QueueCapacity = 1024;
    private const int MaxDrainedPerCycle = 512;

    // Built once from whatever *.Services registers as IPartyResyncRelayHandler at DI-composition time (none
    // yet -- the reconciliation authority is a follow-up piece of this same design). Enumerated eagerly in the
    // primary constructor body: by the time anything resolves PartyResyncRelayHost, every registration the
    // container knows about already exists. A List (not a dictionary) because there is at most one handler
    // today and no Kind discriminator to key on.
    private readonly IReadOnlyList<IPartyResyncRelayHandler> _handlers = handlers.ToArray();

    private readonly Channel<PartyResyncRelayEntry> _outbox =
        Channel.CreateBounded<PartyResyncRelayEntry>(
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
    public bool Enqueue(PartyResyncRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard party-resync relay outbox full on shard {ShardId}; dropping one sort-{Sort} row for " +
            "party {PartyName} (character {SourceCharacterId}) -- a missed resync only leaves the reconnecting " +
            "client's party UI unchanged, no durable state is lost",
            options.Value.ShardId, entry.Sort, entry.PartyName, entry.SourceCharacterId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PartyResyncRelayPollIntervalSeconds));

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed cycle just delays cross-shard party resync by one more poll interval -- never worth
                // crashing the GameServer over (same-shard party membership is entirely unaffected).
                logger.LogError(ex, "Party-resync relay poll failed for shard {ShardId}", options.Value.ShardId);
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
                // SocialCrossShardRelayHost's own failed-publish handling. Only this one cross-shard resync is
                // lost; no durable state is at risk.
                logger.LogError(ex,
                    "Failed to publish a sort-{Sort} party-resync row for party {PartyName} (character " +
                    "{SourceCharacterId}) from shard {ShardId}; this one resync is lost",
                    entry.Sort, entry.PartyName, entry.SourceCharacterId, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.PartyResyncRelayRetentionSeconds;

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
                    "Failed to reconcile relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) on " +
                    "shard {ShardId}",
                    dto.RelayId, dto.Sort, dto.PartyName, shardId);
            }
    }

    private async ValueTask DeliverLocallyAsync(PartyResyncRelayDto dto, CancellationToken ct)
    {
        if (_handlers.Count == 0)
        {
            logger.LogWarning(
                "Relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) has no registered " +
                "IPartyResyncRelayHandler in this composition; dropped -- same-shard party membership is unaffected",
                dto.RelayId, dto.Sort, dto.PartyName);
            return;
        }

        foreach (var handler in _handlers)
            await handler.HandleAsync(dto, ct).ConfigureAwait(false);
    }
}
