namespace Fenrir.Protocol.Center;

public static class PartyEventProtocol
{
    public const int MaxMembers = 5;

    public const int NameFieldLength = 13;

    public const int DataSize = 130;

    public const int InboundPartyNameOffset = 0;
    public const int InboundAvatarNameOffset = NameFieldLength;

    public const int SnapshotPartyNameOffset = 0;
    public const int SnapshotFirstSlotOffset = NameFieldLength;
    public const int SnapshotDispositionOffset = NameFieldLength * (MaxMembers + 1);

    public const int BreakPartyNameOffset = 0;
    public const int BreakAvatarNameOffset = NameFieldLength;
    public const int BreakTrailingSortOffset = NameFieldLength * 2;
}

public enum PartyEventOperation
{
    Join = 104,

    Leave = 106,

    Kick = 107,

    Info = 108,

    Break = 109
}

public enum PartySnapshotDisposition
{
    Created = 1,

    JoinedExisting = 2,

    Refresh = 3
}
