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
