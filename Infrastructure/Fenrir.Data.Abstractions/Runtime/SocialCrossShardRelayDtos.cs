using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum SocialCrossShardRelayKind : byte
{
    Party = 0,
    Friend = 1,
    Mentor = 2,
    Duel = 3,
    Trade = 4,
    GuildInvite = 5
}

public enum SocialCrossShardRelayMessageType : byte
{
    Ask = 0,
    Answer = 1
}

public sealed record SocialCrossShardRelayEntry(
    SocialCrossShardRelayKind Kind,
    SocialCrossShardRelayMessageType MessageType,
    bool? Accepted,
    byte? ReasonCode,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    long? AskRelayId)
{
    // Idempotency token for usp_SocialCrossShardRelay_Publish's retry-safe dedup check -- see
    // GuildTribeBroadcastRelayEntry.CorrelationId's own remarks for the full rationale (generated once at
    // construction, stable across CrossShardRelayRetry's retries of this same entry instance).
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record SocialCrossShardRelayDto(
    long RelayId,
    byte Kind,
    byte MessageType,
    bool? Accepted,
    byte? ReasonCode,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName,
    byte TargetShardId,
    int TargetCharacterId,
    long? AskRelayId);
