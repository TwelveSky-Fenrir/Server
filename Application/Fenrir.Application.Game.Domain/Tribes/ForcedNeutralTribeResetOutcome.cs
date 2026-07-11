namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every distinguishable outcome of using catalog item 8100 (forced-neutral tribe reset). The op23 wire
///     reply only ever exposes two shapes -- <see cref="Success" />, or the single generic "item use failed"
///     reply every other rejected op23 request already shares -- so every non-Success member below collapses
///     to that one generic reply and carries no information back to the client about which rule was violated.
///     Kept granular here purely for logging/testability, mirroring <see cref="TribeMigrationOutcome" />'s own
///     posture for the related-but-distinct op37 mechanism (see <see cref="ForcedNeutralTribeResetGate" />'s
///     own remarks for exactly how the two differ).
/// </summary>
/// <remarks>
///     Réf. "Forced-neutral tribe reset (item 8100 use)" behavior contract (legacy-behavior-translator),
///     itself citing Server/ts25zone/S04_MyWork03.cpp:7225-7281.
/// </remarks>
public enum ForcedNeutralTribeResetOutcome
{
    Success,

    /// <summary>Base level below LV_M1 (113). S04_MyWork03.cpp:7226, Server/Header/Protocol/DEFINE.h:451.</summary>
    LevelTooLow,

    /// <summary>Current tribe is already the neutral faction (3). S04_MyWork03.cpp:7231-7235.</summary>
    AlreadyNeutral,

    /// <summary>
    ///     Holds a tribe office: master, sub-master, or an elected vote candidate for the character's own
    ///     tribe. Server/Header/function.h:92-114 (ReturnTribeRole).
    /// </summary>
    HoldsTribeRole,

    /// <summary>Has a guild, a teacher, or a student on record. S04_MyWork03.cpp:7252-7256.</summary>
    HasGuildOrMentorLink,

    /// <summary>At least one non-empty friend-list slot (MAX_FRIEND_NUM = 10). S04_MyWork03.cpp:7257-7264.</summary>
    HasRegisteredFriends,

    /// <summary>
    ///     The neutral faction's designated home zone/server process is not currently reachable.
    ///     S04_MyWork03.cpp:7266. See
    ///     <see cref="Inventory.UseItems.ForcedNeutralTribeResetUseItemHandler" />'s own remarks for why the
    ///     map id backing this check is operator-configured and defaults to permanently offline.
    /// </summary>
    NeutralHomeZoneOffline
}
