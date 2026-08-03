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

    public int MemberId1 { get; init; }

    public string MemberName1 { get; init; } = "";

    public int MemberId2 { get; init; }

    public string MemberName2 { get; init; } = "";

    public int MemberId3 { get; init; }

    public string MemberName3 { get; init; } = "";

    public int MemberId4 { get; init; }

    public string MemberName4 { get; init; } = "";

    public int MemberId5 { get; init; }

    public string MemberName5 { get; init; } = "";
}

[GenerateDto]
public sealed partial record PartyResyncRelayDto(
    long RelayId,
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName,
    int MemberId1,
    string MemberName1,
    int MemberId2,
    string MemberName2,
    int MemberId3,
    string MemberName3,
    int MemberId4,
    string MemberName4,
    int MemberId5,
    string MemberName5);
