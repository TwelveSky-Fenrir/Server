using System.Collections.Frozen;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting.Relay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class SocialCrossShardRelayHost(
    IEnumerable<ISocialCrossShardRelayHandler> handlers,
    ISocialCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<SocialCrossShardRelayHost> logger)
    : ClusterRelayPumpBase<SocialCrossShardRelayEntry, SocialCrossShardRelayDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.SocialCrossShardRelayPollIntervalSeconds),
            options.Value.SocialCrossShardRelayRetentionSeconds),
        ISocialCrossShardRelayQueue
{
    private const int QueueCapacity = 1024;

    private readonly FrozenDictionary<SocialCrossShardRelayKind, ISocialCrossShardRelayHandler> _handlersByKind =
        handlers.ToFrozenDictionary(static h => h.Kind);

    protected override async ValueTask DeliverAsync(SocialCrossShardRelayDto dto, CancellationToken ct)
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

    protected override void OnOutboxFull(SocialCrossShardRelayEntry entry)
    {
        logger.LogWarning(
            "Cross-shard social relay outbox full on shard {ShardId}; dropping one {Kind}/{MessageType} row " +
            "addressed to character {TargetCharacterId} on shard {TargetShardId} (any same-shard delivery " +
            "already happened, only the cross-shard leg of this one step is lost)",
            options.Value.ShardId, entry.Kind, entry.MessageType, entry.TargetCharacterId, entry.TargetShardId);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Social cross-shard relay outbound flush failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Social cross-shard relay inbound delivery failed for shard {ShardId}",
            options.Value.ShardId);
    }

    protected override void OnPublishFailed(SocialCrossShardRelayEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a {Kind}/{MessageType} row to the cross-shard social relay from shard " +
            "{ShardId} (target character {TargetCharacterId} on shard {TargetShardId}); this one step " +
            "is lost",
            entry.Kind, entry.MessageType, options.Value.ShardId, entry.TargetCharacterId,
            entry.TargetShardId);
    }

    protected override void OnDeliveryFailed(SocialCrossShardRelayDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to locally deliver relayed social row {RelayId} (kind {Kind}, message {MessageType}) " +
            "on shard {ShardId}",
            dto.RelayId, dto.Kind, dto.MessageType, options.Value.ShardId);
    }
}
