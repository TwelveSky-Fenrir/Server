using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Forge;

/// <summary>
///     Pure resolver for the High-Item "Warlord reroll" replacement DRAW (workstream C12-warlord-chest,
///     "part B") -- the piece <see cref="RankChangeResolver" />'s own class-level remarks flagged as
///     deferred ("the Warlord replacement DRAW itself... is deferred"). Consumed only when
///     <see cref="RankChangeResolver.RankChangeResult.WarlordRerollEligible" /> is true: a successful
///     top-tier (encoded level 157), non-cape High-Item upgrade that also passed the cited 75% (rand%4 != 0)
///     draw. This resolver models the per-slot bonus table and the process-wide pity-lock interaction that
///     decide WHICH item that swap grants, not the eligibility decision itself (already modeled by
///     <see cref="RankChangeResolver" />). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:5425-5690 (<c>MyUtil::ReturnWarlordForUpgrade</c>, full
///     function body -- the <c>mGoodWarlord2</c> pity-lock flag, both the rare-tier and elite-tier per-slot
///     bonus tables in full, and the zero-initialized fallthrough for unmatched slot/tier combinations) ;
///     Server/ts25zone/S04_MyWork02.cpp:4079-4134 (<c>HIGH_ITEM_SEND</c> success branch: swap-acceptance
///     check against the ordinary roll's own slot category, the IU-value reset/re-apply sequence, the
///     984-material special case confirmed dead in this build) ; Server/ts25zone/S04_MyWork02.cpp:4092-4095
///     (unconditional notice-broadcast attempt on a top-tier swap) ; Server/Header/function.h:1676-1690
///     (<c>IsWarlord5</c>, the top-tier-range membership check covering both rare-tier and elite-tier top
///     ranges without distinction) ; Server/ts25zone/S04_MyWork02.cpp:309-492 (<c>MakeNotice</c>, specifically
///     its default-case fallback at :482-485 that silently discards any item whose tier value is below elite
///     tier -- the mechanism behind the rare-tier-vs-elite-tier broadcast asymmetry, see
///     <see cref="NoticeReachesRecipients" />).
///     <para>
///         <b>Per-slot id/offset arithmetic derivation:</b> the contract this resolver was built from gives
///         every id and percentage verbatim, but the "tribe offset"/"family offset" formula for the nine
///         weapon-family slot codes is stated only in prose ("the offset instead comes from the specific
///         slot's position within its family group, not from tribe"). The exact per-code offset ORDER
///         (Sword=+0/Blade=+1/Marble=+2, vs. some other order) is an INFERENCE from this codebase's own
///         already-established <see cref="RerollResolver" />'s <c>BuildCategoryList</c> tuple order (the
///         same three-code-per-tribe grouping, same ascending-Sort-code convention), not independently
///         re-cited by this workstream's own contract -- verified self-consistent by reconstructing every one
///         of the six <c>IsWarlord5</c> ranges (87013-87020, 87034-87041, 87055-87062, 87077-87084,
///         87099-87106, 87121-87128) from this table's own base ids/offsets and confirming an exact match,
///         but flagged here as an inference, not a directly-cited fact -- see this workstream's open
///         questions.
///     </para>
///     <para>
///         <b>Tribe-offset domain:</b> <c>previousTribe</c> outside 0-2 is not directly
///         addressed by this workstream's own contract for the reroll table specifically (only for the chest
///         pool, <c>Fenrir.Application.Game.Domain.World.Loot.WarlordChestRewardTable</c> -- a sibling
///         C10-remaining-boxes workstream's own already-shipped implementation of the "part A" chest-content
///         table this workstream's own contract also describes; not duplicated here, see this workstream's
///         notes) -- guarded defensively the same way
///         <see cref="RerollResolver.BuildCategoryList" /> already guards its own identical tribe-array-
///         indexing shape (that method's own doc comment: "a genuine, reproduced legacy gap, not an invented
///         one"), returning <see cref="WarlordRerollOutcome.NoCandidate" /> rather than emitting an
///         out-of-table id.
///     </para>
/// </remarks>
public static class WarlordRerollBonusTable
{
    public enum WarlordRerollOutcome
    {
        /// <summary>
        ///     Slot category has no table entry (cape, or any category outside the eight modeled groups),
        ///     item type is neither Rare nor Elite, or <c>previousTribe</c> is outside 0-2. The swap
        ///     acceptance check at the call site then leaves the original upgrade result untouched -- see
        ///     <see cref="RankChangeResolver" />'s own remarks.
        /// </summary>
        NoCandidate,

        /// <summary>The 5% (rare) / 1% (elite) top-tier outcome. Elite additionally engages the pity lock.</summary>
        Top,

        /// <summary>The upper-mid outcome (41%/16% rare, 15% elite) -- not every slot group has one.</summary>
        Mid,

        /// <summary>The remaining-probability base outcome.</summary>
        Base
    }

    /// <summary>Rare-tier tribe-offset stride (Server/ts25zone/S07_MyGame03.cpp:5425-5690).</summary>
    private const int RareTribeStride = 21;

    /// <summary>Elite-tier tribe-offset stride.</summary>
    private const int EliteTribeStride = 22;

    // Equipment-slot-category codes (Server/Header/Protocol/STRUCT.h:1678-1695), duplicated locally per this
    // workstream's file-isolation constraint (RerollResolver/RankChangeResolver define the identical private
    // consts and cannot be edited to expose them) -- ICape (8) is deliberately absent: the reroll excludes
    // capes by trigger condition AND has no table entry either way.
    private const byte IAmulet = 7;
    private const byte IArmor = 9;
    private const byte IGlove = 10;
    private const byte IRing = 11;
    private const byte IBoots = 12;
    private const byte ISword = 13;
    private const byte IBlade = 14;
    private const byte IMarble = 15;
    private const byte IKatana = 16;
    private const byte IDblade = 17;
    private const byte ILute = 18;
    private const byte ILblade = 19;
    private const byte ISpear = 20;
    private const byte IScepter = 21;

    private static readonly FrozenDictionary<byte, SlotMapping> SlotMappings =
        new Dictionary<byte, SlotMapping>
        {
            [IAmulet] = new(WarlordSlotGroup.Amulet, 0, false),
            [IRing] = new(WarlordSlotGroup.Ring, 0, false),
            [IArmor] = new(WarlordSlotGroup.Armor, 0, false),
            [IGlove] = new(WarlordSlotGroup.Gloves, 0, false),
            [IBoots] = new(WarlordSlotGroup.Boots, 0, false),
            [ISword] = new(WarlordSlotGroup.SwordFamily, 0, true),
            [IBlade] = new(WarlordSlotGroup.SwordFamily, 1, true),
            [IMarble] = new(WarlordSlotGroup.SwordFamily, 2, true),
            [IKatana] = new(WarlordSlotGroup.KatanaFamily, 0, true),
            [IDblade] = new(WarlordSlotGroup.KatanaFamily, 1, true),
            [ILute] = new(WarlordSlotGroup.KatanaFamily, 2, true),
            [ILblade] = new(WarlordSlotGroup.LongBladeFamily, 0, true),
            [ISpear] = new(WarlordSlotGroup.LongBladeFamily, 1, true),
            [IScepter] = new(WarlordSlotGroup.LongBladeFamily, 2, true)
        }.ToFrozenDictionary();

    /// <summary>Rare-tier per-slot bonus table (Server/ts25zone/S07_MyGame03.cpp:5426-5690's rare band).</summary>
    private static readonly FrozenDictionary<WarlordSlotGroup, BonusRow> RareRows =
        new Dictionary<WarlordSlotGroup, BonusRow>
        {
            [WarlordSlotGroup.Amulet] = new(87020, 5, null, 0, 87001),
            [WarlordSlotGroup.Ring] = new(87019, 5, null, 0, 87000),
            [WarlordSlotGroup.Armor] = new(87016, 5, 87006, 41, 87005),
            [WarlordSlotGroup.Gloves] = new(87017, 5, null, 0, 87007),
            [WarlordSlotGroup.Boots] = new(87018, 5, 87012, 16, 87008),
            [WarlordSlotGroup.SwordFamily] = new(87013, 5, 87009, 16, 87002),
            [WarlordSlotGroup.KatanaFamily] = new(87034, 5, 87030, 16, 87023),
            [WarlordSlotGroup.LongBladeFamily] = new(87055, 5, 87051, 16, 87044)
        }.ToFrozenDictionary();

    /// <summary>Elite-tier per-slot bonus table (Server/ts25zone/S07_MyGame03.cpp:5426-5690's elite band).</summary>
    private static readonly FrozenDictionary<WarlordSlotGroup, BonusRow> EliteRows =
        new Dictionary<WarlordSlotGroup, BonusRow>
        {
            [WarlordSlotGroup.Amulet] = new(87084, 1, null, 0, 87064),
            [WarlordSlotGroup.Ring] = new(87083, 1, null, 0, 87063),
            [WarlordSlotGroup.Armor] = new(87080, 1, 87074, 15, 87068),
            [WarlordSlotGroup.Gloves] = new(87081, 1, 87075, 15, 87069),
            [WarlordSlotGroup.Boots] = new(87082, 1, 87076, 15, 87070),
            [WarlordSlotGroup.SwordFamily] = new(87077, 1, 87071, 15, 87065),
            [WarlordSlotGroup.KatanaFamily] = new(87099, 1, 87093, 15, 87087),
            [WarlordSlotGroup.LongBladeFamily] = new(87121, 1, 87115, 15, 87109)
        }.ToFrozenDictionary();

    /// <summary>
    ///     Draws the Warlord-swap replacement item. Only meaningful when the caller has already confirmed
    ///     <see cref="RankChangeResolver.RankChangeResult.WarlordRerollEligible" /> -- this method does not
    ///     re-check the die roll/cape/max-level trigger conditions, those already live in
    ///     <see cref="RankChangeResolver" />.
    /// </summary>
    /// <param name="sort">The upgraded item's own slot category (matches the ordinary roll's result by construction).</param>
    /// <param name="itemType">
    ///     The upgraded item's own tier -- <see cref="RankChangeResolver.RareItemType" /> or
    ///     <see cref="RankChangeResolver.EliteItemType" />.
    /// </param>
    /// <param name="previousTribe">The character's previous-tribe value; must be 0-2 (see this type's own remarks).</param>
    /// <param name="pityLock">
    ///     The zone-shard-wide pity lock (<see cref="WarlordPityLockState" />). Read to decide whether to
    ///     force this draw to <see cref="WarlordPityLockState.ForcedDrawValue" />, and written by this call
    ///     when the elite-tier top-tier outcome is drawn.
    /// </param>
    public static WarlordRerollDrawResult TryDrawReplacement(
        byte sort, byte itemType, byte previousTribe, WarlordPityLockState pityLock, IRandomSource random)
    {
        if (itemType is not (RankChangeResolver.RareItemType or RankChangeResolver.EliteItemType))
            return NoCandidate();

        if (previousTribe > 2)
            return NoCandidate();

        if (!SlotMappings.TryGetValue(sort, out var mapping))
            return NoCandidate();

        var isElite = itemType == RankChangeResolver.EliteItemType;
        var rows = isElite ? EliteRows : RareRows;
        if (!rows.TryGetValue(mapping.Group, out var row))
            return NoCandidate();

        var draw = pityLock.Engaged ? WarlordPityLockState.ForcedDrawValue : random.NextInt32(100);

        var offset = mapping.UsesFamilyOffset
            ? mapping.FamilyOffset
            : previousTribe * (isElite ? EliteTribeStride : RareTribeStride);

        if (draw < row.TopPercent)
        {
            var setsPityLock = isElite;
            if (setsPityLock)
                pityLock.Engage();

            return new WarlordRerollDrawResult(WarlordRerollOutcome.Top, row.TopId + offset, setsPityLock, true);
        }

        if (row.MidId is { } midId && draw < row.TopPercent + row.MidPercent)
            return new WarlordRerollDrawResult(WarlordRerollOutcome.Mid, midId + offset, false, false);

        return new WarlordRerollDrawResult(WarlordRerollOutcome.Base, row.BaseId + offset, false, false);
    }

    /// <summary>
    ///     Whether a top-tier swap notice actually reaches any recipient. The unconditional broadcast
    ///     ATTEMPT at the swap site (S04_MyWork02.cpp:4092-4095) fires for both rare-tier and elite-tier
    ///     top-tier swaps alike, but <c>MakeNotice</c>'s own fallback (S04_MyWork02.cpp:482-485) silently
    ///     discards any item whose stored tier value is below elite tier -- and rare-tier top-range items are
    ///     stored at exactly the "rare" tier value, one below elite (sampling-tier confidence per this
    ///     workstream's own contract). Net effect: only an elite-tier top-tier swap notice ever reaches
    ///     anyone. Exposed here as pure data for whichever broadcast-wiring pass hooks this resolver's output
    ///     into the notice-broadcast path (<c>ZoneEventBroadcaster</c>, out of this workstream's scope) --
    ///     this resolver does not attempt the broadcast itself.
    /// </summary>
    public static bool NoticeReachesRecipients(byte itemType)
    {
        return itemType == RankChangeResolver.EliteItemType;
    }

    private static WarlordRerollDrawResult NoCandidate()
    {
        return new WarlordRerollDrawResult(WarlordRerollOutcome.NoCandidate, 0, false, false);
    }

    private enum WarlordSlotGroup : byte
    {
        Amulet,
        Ring,
        Armor,
        Gloves,
        Boots,
        SwordFamily,
        KatanaFamily,
        LongBladeFamily
    }

    private readonly record struct SlotMapping(WarlordSlotGroup Group, int FamilyOffset, bool UsesFamilyOffset);

    private readonly record struct BonusRow(int TopId, int TopPercent, int? MidId, int MidPercent, int BaseId);

    public readonly record struct WarlordRerollDrawResult(
        WarlordRerollOutcome Outcome,
        int ReplacementItemId,
        bool SetsPityLock,
        bool IsTopTierOutcome);
}
