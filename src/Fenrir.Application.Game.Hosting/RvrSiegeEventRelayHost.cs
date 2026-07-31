using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Cluster.Client.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class RvrSiegeEventRelayHost(
    Lazy<ZoneCenterBroadcastIngestor> ingestor,
    Lazy<ZoneEventBroadcaster> broadcaster,
    IRvrSiegeEventRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<RvrSiegeEventRelayHost> logger)
    : ClusterRelayPumpBase<RvrSiegeEventRelayEntry, RvrSiegeEventRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.RvrSiegeEventRelayPollIntervalSeconds),
            options.Value.RvrSiegeEventRelayRetentionSeconds),
        IRvrSiegeEventRelayQueue
{
    private const int Zone049RangeStart = 1;

    private const int Zone049RangeEnd = 9;

    private const int QueueCapacity = 256;

    protected override ValueTask DeliverAsync(RvrSiegeEventRelayDto dto, CancellationToken ct)
    {
        DeliverLocally(dto);
        return ValueTask.CompletedTask;
    }

    private void DeliverLocally(RvrSiegeEventRelayDto dto)
    {
        if (dto.Sort is >= Zone049RangeStart and <= Zone049RangeEnd)
            ingestor.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
        else
            broadcaster.Value.ApplyRelayedEvent(dto.Sort, dto.Data);
    }

    protected override void OnOutboxFull(RvrSiegeEventRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard rvr-siege relay outbox full on shard {ShardId}; dropping the cross-shard fan-out for " +
            "sort {Sort} (same-shard delivery already happened, only the cross-shard leg is lost)",
            options.Value.ShardId, entry.Sort);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "RvR-siege relay outbound flush failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "RvR-siege relay inbound delivery failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnPublishFailed(RvrSiegeEventRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish an rvr-siege relay row (sort {Sort}) from shard {ShardId}; cross-shard " +
            "fan-out for this one event is lost (same-shard delivery already happened)",
            entry.Sort, options.Value.ShardId);
    }

    protected override void OnDeliveryFailed(RvrSiegeEventRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to locally deliver relayed rvr-siege event {RelayId} (sort {Sort}) on shard {ShardId}",
            dto.RelayId, dto.Sort, options.Value.ShardId);
    }
}
