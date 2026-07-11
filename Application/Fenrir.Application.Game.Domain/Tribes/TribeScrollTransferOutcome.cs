namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Every distinguishable outcome of using a Faction Transfer Scroll (world.Items 8153/8154). The op23 wire
///     reply only ever exposes three shapes: a genuine session disconnect (no response at all), the single
///     generic "item use failed" reply every other rejected op23 request already shares, or success. Kept
///     granular here purely for logging/testability, mirroring <see cref="ForcedNeutralTribeResetOutcome" />'s
///     own posture for the related-but-distinct item-8100 mechanism (see
///     <see cref="TribeScrollTransferGate" />'s own remarks for exactly how the two differ).
/// </summary>
/// <remarks>
///     Réf. "Faction Transfer scroll (items 8153/8154) runtime gates + TChangeTribe mutation" behavior contract
///     (legacy-behavior-translator), itself citing Server/ts25zone/S04_MyWork03.cpp:4740-4841 (the ordered gate
///     chain) throughout.
/// </remarks>
public enum TribeScrollTransferOutcome
{
    Success,

    /// <summary>
    ///     Client-supplied destination tribe outside the playable 0-2 range. Legacy's own bound check here is
    ///     coded as "value &lt; 0 AND value &gt; 2", a condition no whole number can ever satisfy -- dead in
    ///     effect. Fenrir closes this real hardening gap with an actual range check
    ///     (<see cref="TribeConversionResolver.IsPlayableTribe" />) rather than reproducing the inert legacy
    ///     check verbatim. S04_MyWork03.cpp:4745-4749.
    /// </summary>
    InvalidDestinationTribe,

    /// <summary>
    ///     Destination tribe already equals the character's own PreviousTribe -- unconditional, no neutral
    ///     exemption (unlike the sibling tribe-conversion BOOK mechanic). S04_MyWork03.cpp:4750-4754.
    /// </summary>
    AlreadyTargetTribe,

    /// <summary>Base level below LV_M33 (145, an exact-equality gate in effect only because 145 is also the level cap -- see <see cref="TribeScrollTransferGate" />'s own remarks). S04_MyWork03.cpp:4756-4763, Server/Header/Protocol/DEFINE.h:483.</summary>
    LevelTooLow,

    /// <summary>
    ///     The neutral, tribe-agnostic "town" zone/server (gate 5's own numbered zone/server slot) is not
    ///     currently reachable by any live shard. S04_MyWork03.cpp:4775-4782. See
    ///     <see cref="Inventory.UseItems.TribeScrollTransferUseItemHandler" />'s own remarks for why the map id
    ///     backing this check is operator-configured rather than hardcoded.
    /// </summary>
    HomeZoneOffline,

    /// <summary>
    ///     The character is not physically standing in its own CURRENT tribe's capital town (IsValidTown).
    ///     S04_MyWork03.cpp:4783-4790, Server/Header/mapcheck.h:83-114.
    /// </summary>
    WrongLocation,

    /// <summary>
    ///     Holds a tribe office: master, sub-master, or an elected vote candidate for the character's own
    ///     tribe. Server/Header/function.h:92-114 (ReturnTribeRole). S04_MyWork03.cpp:4791-4798.
    /// </summary>
    HoldsTribeRole,

    /// <summary>Currently registered as anyone's mentor ("teacher") -- fails silently in legacy, no chat message. S04_MyWork03.cpp:4799-4803.</summary>
    IsMentor,

    /// <summary>Currently registered as anyone's mentee ("student") -- fails silently in legacy, no chat message. S04_MyWork03.cpp:4804-4808.</summary>
    IsMentee,

    /// <summary>Currently in a party. S04_MyWork03.cpp:4809-4816.</summary>
    InParty,

    /// <summary>Currently in a guild. S04_MyWork03.cpp:4817-4824.</summary>
    HasGuild,

    /// <summary>
    ///     Something is equipped in the cape/cloak slot -- the mutation's own equip remap structurally excludes
    ///     that slot (a cape can never be converted across tribes), so the transfer refuses to start at all
    ///     while one is equipped. S04_MyWork03.cpp:4825-4832.
    /// </summary>
    CapeEquipped,

    /// <summary>At least one non-empty friend-list slot -- fails silently in legacy, no chat message. S04_MyWork03.cpp:4833-4840.</summary>
    HasRegisteredFriends
}
