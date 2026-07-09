using System.Threading.Channels;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Cross-shard fan-out, Game-side, for the rvr-siege world-event family: the Zone049 siege-zone-slot
///     sub-codes (1-9, owned by <see cref="ZoneCenterBroadcastIngestor" />) and the tribe-symbol/alliance tSort
///     family (38/39/40/42/45/46/47, owned by <see cref="ZoneEventBroadcaster" />). Both producers already
///     mutate this shard's own shared state and broadcast to this shard's own hosted zones synchronously and
///     unchanged BEFORE ever calling <see cref="Enqueue" /> -- see either class's own CROSS-SHARD RELAY remarks
///     for why that same-shard leg alone is no longer enough once <see cref="Fenrir.Application.Game.Domain.World.ZoneRegistry" />
///     only ever covers a disjoint subset of the game's maps.
/// </summary>
/// <remarks>
///     <para>
///         Same "one instance, three registrations" composition shape as <c>GuildTribeBroadcastRelayHost</c>: the
///         two rvr-siege producers consume <see cref="IRvrSiegeEventRelayQueue" /> only, immediately after each
///         one's own unchanged, synchronous same-shard mutation/broadcast.
///     </para>
///     <para>
///         Unlike every sibling relay host in this cluster, the two delivery targets
///         (<see cref="ZoneCenterBroadcastIngestor" />/<see cref="ZoneEventBroadcaster" />) are themselves the
///         producers, so a direct constructor dependency on either would create a DI cycle
///         (Host -&gt; Ingestor/Broadcaster -&gt; <see cref="IRvrSiegeEventRelayQueue" /> -&gt; Host). Both are
///         instead resolved lazily (<see cref="Lazy{T}" />, only touched inside <see cref="DeliverLocallyAsync" />,
///         well after the whole DI graph has finished constructing) -- same opaque-to-the-cycle-checker pattern
///         <c>HostingServiceCollectionExtensions.AddZoneWar</c> already established for
///         <c>MonsterSpawnScheduler</c>'s own dependency on <see cref="ZoneEventBroadcaster" />.
///     </para>
///     <para>
///         Every poll cycle does two things, in order: (1) drains this shard's own outbound queue and publishes
///         each entry via <see cref="IRvrSiegeEventRelayRepository.PublishAsync" /> so every OTHER live shard's
///         own next poll picks it up; (2) calls <see cref="IRvrSiegeEventRelayRepository.PollAsync" /> for rows
///         some OTHER shard published since this shard's own last poll and replays each through
///         <see cref="ZoneCenterBroadcastIngestor.ApplyRelayedEvent" /> (Zone049 sub-codes 1-9) or
///         <see cref="ZoneEventBroadcaster.ApplyRelayedEvent" /> (every other sort this relay ever carries). A
///         shard never re-delivers its own published rows to itself -- the originating producer already
///         delivered synchronously to this shard's own hosted zones before ever enqueuing here, and the poll's
///         own SQL predicate excludes <c>SourceShardId = @ShardId</c>.
///     </para>
///     <para>
///         Per-entry/per-row isolation on both the publish and delivery loops, same posture as every sibling
///         relay host in this cluster.
///     </para>
/// </remarks>
public sealed class RvrSiegeEventRelayHost(
    Lazy<ZoneCenterBroadcastIngestor> ingestor,
    Lazy<ZoneEventBroadcaster> broadcaster,
    IRvrSiegeEventRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<RvrSiegeEventRelayHost> logger) : BackgroundService, IRvrSiegeEventRelayQueue
{
    /// <summary>Zone049 owns sub-codes 1-9; every other sort this relay ever carries belongs to ZoneEventBroadcaster.</summary>
    private const int Zone049RangeStart = 1;

    private const int Zone049RangeEnd = 9;

    // Rare, human/schedule-cadence traffic (siege-slot transitions, tribe-symbol-battle phase changes, alliance
    // offers) -- generous headroom over any plausible per-cycle burst, same sizing rationale as
    // GuildBuffExpiryRelayHost's own smaller outbox (not GuildTribeBroadcastRelayHost's chat-volume sizing).
    private const int QueueCapacity = 256;

    private const int MaxDrainedPerCycle = 128;

    private readonly Channel<RvrSiegeEventRelayEntry> _outbox = Channel.CreateBounded<RvrSiegeEventRelayEntry>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            // Wait mode's TryWrite returns false immediately when full -- the honest, non-blocking backpressure
            // signal Enqueue needs, since it only ever calls TryWrite. Same reasoning as every sibling relay
            // host's own choice of Wait over DropWrite.
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <inheritdoc />
    public bool Enqueue(RvrSiegeEventRelayEntry entry)
    {
        if (_outbox.Writer.TryWrite(entry))
            return true;

        logger.LogWarning(
            "Cross-shard rvr-siege relay outbox full on shard {ShardId}; dropping the cross-shard fan-out for " +
            "sort {Sort} (same-shard delivery already happened, only the cross-shard leg is lost)",
            options.Value.ShardId, entry.Sort);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.RvrSiegeEventRelayPollIntervalSeconds));

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
                logger.LogError(ex, "RvR-siege relay poll failed for shard {ShardId}", options.Value.ShardId);
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
                // relay host: this shard's OWN players already saw this event synchronously, only the
                // other-shard fan-out for this one message is lost.
                logger.LogError(ex,
                    "Failed to publish an rvr-siege relay row (sort {Sort}) from shard {ShardId}; cross-shard " +
                    "fan-out for this one event is lost (same-shard delivery already happened)",
                    entry.Sort, options.Value.ShardId);
            }
        }
    }

    private async ValueTask DeliverInboundAsync(CancellationToken ct)
    {
        var shardId = options.Value.ShardId;
        var retentionSeconds = options.Value.RvrSiegeEventRelayRetentionSeconds;

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
                    "Failed to locally deliver relayed rvr-siege event {RelayId} (sort {Sort}) on shard {ShardId}",
                    dto.RelayId, dto.Sort, shardId);
            }
    }

    private void DeliverLocally(RvrSiegeEventRelayDto dto)
    {
        if (dto.Sort is >= Zone049RangeStart and <= Zone049RangeEnd)
            ingestor.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
        else
            broadcaster.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
    }
}
