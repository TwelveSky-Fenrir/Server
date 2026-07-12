using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record ChatCrossShardWhisperEntry(
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType)
{
    // Idempotency token for usp_ChatCrossShardRelay_Publish's retry-safe dedup check -- see
    // GuildTribeBroadcastRelayEntry.CorrelationId's own remarks for the full rationale (generated once at
    // construction, stable across CrossShardRelayRetry's retries of this same entry instance).
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record ChatCrossShardWhisperDto(
    long RelayId,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    string Content,
    byte SenderAuthType);
