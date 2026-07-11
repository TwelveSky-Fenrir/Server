using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- Ornament (gold/silver) fixed stat bonuses (WORKSTREAM B7) ----
    //
    // Legacy: MyFactor::IsUsedOrnament + MyFactor::GetUsedOrnament (Server/Header/Protocol/MyFactor.cpp:513-547).
    // An armed ornament folds one flat, table-selected whole number into each of the four BASE stats -- Max Life
    // (ORN_HP), Max Mana (ORN_MP), Attack Power (ORN_DMG) and Defense Power (ORN_DEF). There is no scaling, packed
    // decode, or interpolation: the returned value is a constant picked purely by (active tier, requested stat).
    //
    // The four-part activation gate (IsUsedOrnament, MyFactor.cpp:513-521): the ornament is armed only when the
    // active flag is exactly 1 AND each of the four decoration equipment slots (array entries 9-12 = EDECO1..4,
    // Server/Header/Protocol/STRUCT.h:1662-1676) holds an item id greater than zero -- NOT the amulet slot (0) or
    // the pet slot (8). Inside that gate a dangling-else gives gold strict priority over silver: if the gold
    // remaining-time counter is positive the tier is gold regardless of silver; silver is chosen only when gold is
    // not positive and silver is; otherwise the ornament is not active. An unknown stat selector, an inactive
    // gate, or a non-1 active flag all silently contribute 0.
    //
    // LAYER: BASE cache (recompute-on-equip/state-change, MyFactor.cpp:1922-1926 / :2239-2243 / :2436-2437 /
    // :2749-2755). This is NOT recomputed per combat tick. The countdown/self-disable half of the contract
    // (S07_MyGame04.cpp:1275-1296 -- the 120-tick timer that decrements the active tier's counter, broadcasts it,
    // and permanently clears the active flag when both counters reach zero) is a Zone-tick concern, not a stat
    // computation, so it is out of this Stats slice -- see openQuestions for its hand-off.
    //
    // INPUT NOTE (active flag): the contract's active flag is a whole number that legacy arms only on the value
    // exactly 1 (no other value, including other positive values, arms it). This slice consumes it via the
    // existing B1 ZoneContext.OrnamentInUse (a bool), which relies on the assembly step mapping aUseOrnament == 1
    // to true -- see openQuestions. The two remaining-time counters are read from ZoneContext's
    // OrnamentGoldTimeRemaining / OrnamentSilverTimeRemaining.

    /// <summary>
    ///     Which stat's ornament bonus is being requested -- the legacy <c>ORNAMENT_SORT</c> enum
    ///     (Server/Header/Protocol/MyFactor.cpp:41-46). The numeric values match the legacy codes and are the keys
    ///     of <see cref="OrnamentBonusTable" />.
    /// </summary>
    public enum OrnamentSort
    {
        /// <summary>ORN_HP (0) -- Max Life.</summary>
        Hp = 0,

        /// <summary>ORN_MP (1) -- Max Mana.</summary>
        Mp = 1,

        /// <summary>ORN_DMG (2) -- Attack Power.</summary>
        Dmg = 2,

        /// <summary>ORN_DEF (3) -- Defense Power.</summary>
        Def = 3
    }

    /// <summary>
    ///     The tier resolved by the four-part activation gate (<see cref="ResolveOrnamentTier" />). Gold has strict
    ///     priority over silver; <see cref="NotActive" /> covers both "gate failed" and "both counters non-positive".
    /// </summary>
    public enum OrnamentTier
    {
        /// <summary>Gate failed, active flag not exactly 1, or neither remaining-time counter is positive.</summary>
        NotActive = 0,

        /// <summary>Gold remaining-time counter positive -- the higher fixed bonus tier (strict priority).</summary>
        Gold = 1,

        /// <summary>Gold not positive and silver remaining-time counter positive -- the lower fixed bonus tier.</summary>
        Silver = 2
    }

    /// <summary>
    ///     The fixed (stat, tier) ornament bonus table. Gold values MyFactor.cpp:530-533 (825/750/413/825), silver
    ///     values MyFactor.cpp:540-543 (550/500/215/550). Flat whole numbers -- no scaling, packed decode, or
    ///     interpolation. Built once at type init and read on every base-stat recompute.
    /// </summary>
    public static readonly FrozenDictionary<OrnamentSort, OrnamentBonusRow> OrnamentBonusTable =
        new Dictionary<OrnamentSort, OrnamentBonusRow>
        {
            [OrnamentSort.Hp] = new(825, 550),
            [OrnamentSort.Mp] = new(750, 500),
            [OrnamentSort.Dmg] = new(413, 215),
            [OrnamentSort.Def] = new(825, 550)
        }.ToFrozenDictionary();

    /// <summary>
    ///     Equipment-array indices of the four decoration slots EDECO1..EDECO4 the activation gate requires to be
    ///     occupied (item id &gt; 0) -- Server/Header/Protocol/STRUCT.h:1662-1676. Deliberately excludes the amulet
    ///     slot (0) and the pet slot (8), which the gate does NOT require.
    /// </summary>
    public static readonly FrozenSet<int> OrnamentDecorationSlots = new[] { 9, 10, 11, 12 }.ToFrozenSet();

    /// <summary>
    ///     Resolves the active ornament tier from the four-part activation gate (MyFactor::IsUsedOrnament,
    ///     MyFactor.cpp:513-521): the active flag must be armed and every one of the four decoration slots (9-12)
    ///     must hold an item id greater than zero; then a gold-priority dangling-else selects the tier. Returns
    ///     <see cref="OrnamentTier.NotActive" /> if any part of the gate fails or neither remaining-time counter is
    ///     positive. Pure read -- no side effect.
    /// </summary>
    public static OrnamentTier ResolveOrnamentTier(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        if (!zone.OrnamentInUse)
            return OrnamentTier.NotActive;

        foreach (var slotIndex in OrnamentDecorationSlots)
            if (bySlot[slotIndex] is not { } slot || slot.Item.ItemId <= 0)
                return OrnamentTier.NotActive;

        // Gold beats silver strictly (the dangling-else binds to the gold test): gold if its counter is positive,
        // else silver if its counter is positive, else not active.
        if (zone.OrnamentGoldTimeRemaining > 0)
            return OrnamentTier.Gold;
        if (zone.OrnamentSilverTimeRemaining > 0)
            return OrnamentTier.Silver;
        return OrnamentTier.NotActive;
    }

    /// <summary>
    ///     The flat ornament bonus for one (stat, tier) pair (MyFactor::GetUsedOrnament, MyFactor.cpp:523-547). An
    ///     unknown selector (a code outside the four <see cref="OrnamentSort" /> values) or an inactive tier both
    ///     yield 0.
    /// </summary>
    public static int GetOrnamentBonus(OrnamentSort sort, OrnamentTier tier)
    {
        if (!OrnamentBonusTable.TryGetValue(sort, out var row))
            return 0;

        return tier switch
        {
            OrnamentTier.Gold => row.Gold,
            OrnamentTier.Silver => row.Silver,
            _ => 0
        };
    }

    /// <summary>Flat ORN_HP ornament term for the Max Life base (MyFactor.cpp:1922-1926). 0 when not armed.</summary>
    public static int OrnamentLifeContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Hp, ResolveOrnamentTier(zone, bySlot));
    }

    /// <summary>Flat ORN_MP ornament term for the Max Mana base (MyFactor.cpp:2239-2243). 0 when not armed.</summary>
    public static int OrnamentManaContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Mp, ResolveOrnamentTier(zone, bySlot));
    }

    /// <summary>Flat ORN_DMG ornament term for the Attack Power base (MyFactor.cpp:2436-2437). 0 when not armed.</summary>
    public static int OrnamentAttackContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Dmg, ResolveOrnamentTier(zone, bySlot));
    }

    /// <summary>Flat ORN_DEF ornament term for the Defense Power base (MyFactor.cpp:2749-2755). 0 when not armed.</summary>
    public static int OrnamentDefenseContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Def, ResolveOrnamentTier(zone, bySlot));
    }

    /// <summary>One ornament stat's two fixed tier bonuses (MyFactor.cpp:530-533 gold / :540-543 silver).</summary>
    public readonly record struct OrnamentBonusRow(int Gold, int Silver);
}
