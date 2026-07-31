using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.Relay;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class ChatCrossShardRelayHost(
    ZoneRegistry zones,
    IChatCrossShardRelayRepository relay,
    IOptions<GameServerOptions> options,
    ILogger<ChatCrossShardRelayHost> logger)
    : ClusterRelayPumpBase<ChatCrossShardWhisperEntry, ChatCrossShardWhisperDto>(
            relay,
            options.Value.ShardId,
            QueueCapacity,
            TimeSpan.FromSeconds(options.Value.ChatCrossShardRelayPollIntervalSeconds),
            options.Value.ChatCrossShardRelayRetentionSeconds),
        IChatCrossShardRelayQueue
{
    private const int QueueCapacity = 1024;

    private static readonly ItemLinkInfo EmptyLink =
        new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    protected override ValueTask DeliverAsync(ChatCrossShardWhisperDto dto, CancellationToken ct)
    {
        DeliverLocally(dto);
        return ValueTask.CompletedTask;
    }

    private void DeliverLocally(ChatCrossShardWhisperDto dto)
    {
        if (!zones.TryGetPlayer(dto.TargetCharacterId, out var target) || target.IsMovingZone)
        {
            logger.LogDebug(
                "Relayed whisper {RelayId} from {SourceName} to character {TargetCharacterId} on shard {ShardId} " +
                "found no deliverable local target (departed or mid zone-transfer); dropped",
                dto.RelayId, dto.SourceAvatarName, dto.TargetCharacterId, options.Value.ShardId);
            return;
        }

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

    protected override void OnOutboxFull(ChatCrossShardWhisperEntry entry)
    {
        logger.LogWarning(
            "Cross-shard whisper relay outbox full on shard {ShardId}; dropping one whisper from {SourceName} " +
            "to {TargetName} (character {TargetCharacterId} on shard {TargetShardId}) -- the sender already saw " +
            "the accepted acknowledgement, only this one cross-shard delivery is lost",
            options.Value.ShardId, entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId,
            entry.TargetShardId);
    }

    protected override void OnOutboundFlushFailed(Exception ex)
    {
        logger.LogError(ex, "Cross-shard whisper outbound flush failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnInboundDeliveryFailed(Exception ex)
    {
        logger.LogError(ex, "Cross-shard whisper inbound delivery failed for shard {ShardId}", options.Value.ShardId);
    }

    protected override void OnPublishFailed(ChatCrossShardWhisperEntry entry, Exception ex)
    {
        logger.LogError(ex,
            "Failed to publish a cross-shard whisper from {SourceName} to {TargetName} (character " +
            "{TargetCharacterId} on shard {TargetShardId}) from shard {ShardId}; this one whisper is lost",
            entry.SourceAvatarName, entry.TargetAvatarName, entry.TargetCharacterId, entry.TargetShardId,
            options.Value.ShardId);
    }

    protected override void OnDeliveryFailed(ChatCrossShardWhisperDto dto, Exception ex)
    {
        logger.LogError(ex,
            "Failed to locally deliver relayed whisper {RelayId} (from {SourceName} to {TargetName}) on " +
            "shard {ShardId}",
            dto.RelayId, dto.SourceAvatarName, dto.TargetAvatarName, options.Value.ShardId);
    }
}
