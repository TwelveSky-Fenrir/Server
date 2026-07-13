namespace Fenrir.Cluster.Party;

/// <summary>
///     Byte-exact constants and sub-code enums for the op57 authoritative party protocol. All values are
///     legacy-cited (DEFINE.h / STRUCT.h) — none are Fenrir product choices.
/// </summary>
public static class PartyEventProtocol
{
    /// <summary><c>MAX_PARTY_AVATAR_NUM</c> (DEFINE.h:610): a roster holds slot 0 (leader) + up to 4 members.</summary>
    public const int MaxMembers = 5;

    /// <summary><c>MAX_AVATAR_NAME_LENGTH</c> / <c>MAX_PARTY_NAME_LENGTH</c> (DEFINE.h:280/308): 13-byte, null-terminated, ≤12 usable chars.</summary>
    public const int NameFieldLength = 13;

    /// <summary><c>MAX_BROADCAST_DATA_SIZE</c> (DEFINE.h:377-381, <c>USE_ITEM_LINK_V2</c> unconditional): the fixed tData region, always transmitted in full.</summary>
    public const int DataSize = 130;

    // tData field offsets — inbound (partyName + avatarName + zero pad).
    public const int InboundPartyNameOffset = 0;
    public const int InboundAvatarNameOffset = NameFieldLength; // 13

    // tData field offsets — snapshot PARTY_INFO_RECV (partyName + 5 slot names + pSort + zero pad).
    public const int SnapshotPartyNameOffset = 0;
    public const int SnapshotFirstSlotOffset = NameFieldLength;                     // 13
    public const int SnapshotDispositionOffset = NameFieldLength * (MaxMembers + 1); // 78

    // tData field offsets — break PARTY_BREAK_RECV (partyName + avatarName01 + trailing sort + zero pad).
    public const int BreakPartyNameOffset = 0;
    public const int BreakAvatarNameOffset = NameFieldLength;      // 13
    public const int BreakTrailingSortOffset = NameFieldLength * 2; // 26
}

/// <summary>
///     The op57 party sub-code carried in the envelope <c>tSort</c>. Only these five are meaningful; any other
///     value is an unknown sub-code (raw-echoed then ignored, per the contract).
/// </summary>
public enum PartyEventOperation
{
    /// <summary>104 — a member joins (creating the party if its key is absent).</summary>
    Join = 104,

    /// <summary>106 — a member leaves voluntarily. Indistinguishable from <see cref="Kick" /> at the Center.</summary>
    Leave = 106,

    /// <summary>107 — a member is kicked. Indistinguishable from <see cref="Leave" /> at the Center.</summary>
    Kick = 107,

    /// <summary>108 — a refresh/re-sync query (e.g. a member re-entering a zone). The one sub-code that is never raw-echoed.</summary>
    Info = 108,

    /// <summary>109 — the party is disbanded; its whole entry is erased.</summary>
    Break = 109,
}

/// <summary>
///     Snapshot disposition (<c>pSort</c>) accompanying a 108 fan-out — live legacy values
///     (S04_MyWork02.cpp:1373 / 1392 / 1458).
/// </summary>
public enum PartySnapshotDisposition
{
    /// <summary>1 — this JOIN newly created the party.</summary>
    Created = 1,

    /// <summary>2 — this JOIN added a member to an already-existing party.</summary>
    JoinedExisting = 2,

    /// <summary>3 — answer to a 108 INFO query (a member re-syncing on zone re-entry).</summary>
    Refresh = 3,
}
