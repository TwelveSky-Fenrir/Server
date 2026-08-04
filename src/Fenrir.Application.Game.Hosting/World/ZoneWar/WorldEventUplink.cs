using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class WorldEventUplink(
    ILogger<WorldEventUplink> logger,
    IZoneEventRelayOutboxRepository? outbox = null,
    IOptions<GameServerOptions>? gameOptions = null,
    IZoneEventRelayOutboxWakeSignal? wakeSignal = null) : IWorldEventUplink
{
    public WorldEventUplinkResult Publish(int sort, ReadOnlySpan<byte> data,
        WorldEventPublicationIdentity? suppliedIdentity = null)
    {
        var identity = suppliedIdentity ?? WorldEventPublicationIdentity.Create();
        if (!identity.IsValid)
        {
            logger.LogError("World event uplink rejected sort {Sort}: the operation identity is invalid", sort);
            return WorldEventUplinkResult.Faulted(identity);
        }

        if (outbox is null || gameOptions is null)
        {
            logger.LogError("World event uplink faulted for sort {Sort}: durable outbox services are unavailable", sort);
            return WorldEventUplinkResult.Faulted(identity);
        }

        if (!KnownTSortRegistry.IsKnown(sort) || data.Length != ZoneCenterBroadcastIngestor.PayloadSize)
        {
            logger.LogWarning("World event uplink rejected sort {Sort}: the fixed zone-event envelope is invalid", sort);
            return WorldEventUplinkResult.Faulted(identity);
        }

        try
        {
            var shardId = gameOptions.Value.ShardId;
            var result = outbox.EnqueueAsync(new ZoneEventRelayOutboxEntry(shardId, sort, data.ToArray(),
                    identity.OperationId, identity.CorrelationId), CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();

            if (result.IsAccepted)
            {
                wakeSignal?.Signal();
                return WorldEventUplinkResult.Enqueued(identity);
            }

            logger.LogWarning(
                "World event uplink backpressured on shard {ShardId} for sort {Sort} operation {OperationId}: durable relay capacity is full",
                shardId, sort, identity.OperationId);
            return WorldEventUplinkResult.Backpressured(identity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "World event uplink faulted for sort {Sort} operation {OperationId}: durable relay persistence was not confirmed",
                sort, identity.OperationId);
            return WorldEventUplinkResult.Faulted(identity);
        }
    }
}
