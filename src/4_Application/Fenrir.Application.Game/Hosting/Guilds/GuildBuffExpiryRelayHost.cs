using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Cluster.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Guilds;

public sealed class GuildBuffExpiryRelayHost(
    ZoneRegistry zones,
    IGuildBuffExpiryRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildBuffExpiryRelayHost> logger)
    : ClusterRelayPumpBase<GuildBuffExpiryRelayEntry, GuildBuffExpiryRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.GuildBuffExpiryRelayPollIntervalSeconds),
            options.Value.GuildBuffExpiryRelayRetentionSeconds),
        IGuildBuffExpiryRelayQueue
{
    private const int QueueCapacity = 64;

    protected override ValueTask DeliverAsync(GuildBuffExpiryRelayDto dto, CancellationToken ct)
    {
        var command = new GuildBuffExpiryZoneCommand(dto.GuildId, dto.NewBuffTime);
        foreach (var zone in zones.Zones)
            zone.PostGuildBuffExpiryCommand(in command);

        return ValueTask.CompletedTask;
    }

    protected override void OnOutboxFull(GuildBuffExpiryRelayEntry entry) =>
        logger.LogWarning(
            "Cross-shard guild-buff-expiry relay outbox full on shard {ShardId}; dropping the fan-out for " +
            "guild {GuildId} (same-shard delivery already happened, only the cross-shard fan-out is lost)",
            options.Value.ShardId, entry.GuildId);

    protected override void OnOutboundFlushFailed(Exception ex) =>
        logger.LogError(ex, "Guild-buff-expiry relay outbound flush failed for shard {ShardId}",
            options.Value.ShardId);

    protected override void OnInboundDeliveryFailed(Exception ex) =>
        logger.LogError(ex, "Guild-buff-expiry relay inbound delivery failed for shard {ShardId}",
            options.Value.ShardId);

    protected override void OnPublishFailed(GuildBuffExpiryRelayEntry entry, Exception ex) =>
        logger.LogError(ex,
            "Failed to publish a guild-buff-expiry push for guild {GuildId} to the cross-shard relay " +
            "from shard {ShardId}; cross-shard fan-out for this one push is lost (same-shard delivery " +
            "already happened)", entry.GuildId, options.Value.ShardId);

    protected override void OnDeliveryFailed(GuildBuffExpiryRelayDto dto, Exception ex) =>
        logger.LogError(ex,
            "Failed to locally deliver relayed guild-buff-expiry push {RelayId} (guild {GuildId}) on shard {ShardId}",
            dto.RelayId, dto.GuildId, options.Value.ShardId);
}
