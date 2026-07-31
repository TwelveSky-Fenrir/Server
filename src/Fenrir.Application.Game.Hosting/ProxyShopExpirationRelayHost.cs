using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Cluster.Client.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class ProxyShopExpirationRelayHost(
    ZoneRegistry zones,
    IProxyShopExpirationRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ProxyShopExpirationRelayHost> logger)
    : ClusterRelayPumpBase<ProxyShopExpirationRelayEntry, ProxyShopExpirationRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.ProxyShopExpirationRelayPollIntervalSeconds),
            options.Value.ProxyShopExpirationRelayRetentionSeconds),
        IProxyShopExpirationRelayQueue
{
    private const int QueueCapacity = 1024;

    protected override ValueTask DeliverAsync(ProxyShopExpirationRelayDto dto, CancellationToken ct)
    {
        DeliverLocally(dto);
        return ValueTask.CompletedTask;
    }

    private void DeliverLocally(ProxyShopExpirationRelayDto dto)
    {
        foreach (var zone in zones.Zones)
        {
            if (zone.MapId != ProxyShopZonePolicy.ZoneNumber)
                continue;

            zone.TryUpdateProxyShopExpiration(dto.CharacterId, dto.NewExpirationDate);
            return;
        }
    }

    protected override void OnOutboxFull(ProxyShopExpirationRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard proxy-shop expiration relay outbox full on shard {ShardId}; dropping the cross-shard " +
            "leg of one rental extension for character {CharacterId} (the durable game.OfflineShops.ShopDate " +
            "write already succeeded either way)",
            options.Value.ShardId, entry.CharacterId);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Proxy-shop expiration relay outbound flush failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Proxy-shop expiration relay inbound delivery failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnPublishFailed(ProxyShopExpirationRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a proxy-shop expiration relay row for character {CharacterId} from " +
            "shard {ShardId}; the cross-shard leg of this one extension is lost (the durable date is " +
            "already saved)",
            entry.CharacterId, options.Value.ShardId);
    }

    protected override void OnDeliveryFailed(ProxyShopExpirationRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to locally deliver relayed proxy-shop expiration update {RelayId} for character " +
            "{CharacterId} on shard {ShardId}",
            dto.RelayId, dto.CharacterId, options.Value.ShardId);
    }
}
