using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum PartyResyncRelaySort : byte
{
    Request = 108,

    PartyInfoReply = 110,

    PartyBreak = 109,

    LeaveNotice = 106,

    KickNotice = 107,

    DisbandNotice = 111
}

public sealed record PartyResyncRelayEntry(
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName)
{
    // Idempotency token for usp_PartyResyncRelay_Publish's retry-safe dedup check -- see
    // GuildTribeBroadcastRelayEntry.CorrelationId's own remarks for the full rationale (generated once at
    // construction, stable across CrossShardRelayRetry's retries of this same entry instance).
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record PartyResyncRelayDto(
    long RelayId,
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName);
