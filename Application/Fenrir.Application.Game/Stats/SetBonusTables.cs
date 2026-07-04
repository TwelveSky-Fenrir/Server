using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Per-item-stat coefficient row applied by every GetBase* formula's equipment loop (report 11 §7.3):
///     "the 6 tables dupliquées" the legacy repeats verbatim inside GetBaseAttackPower/DefensePower/
///     AttackSuccess/AttackBlock/ElementAttackPower/ElementDefensePower, consolidated here into one row per
///     set number, plus the reduced Critical/CriticalDefence/Luck columns that a few sets also feed.
/// </summary>
public readonly record struct SetCoefficients(
    float AttackPower,
    float DefensePower,
    float AttackSuccess,
    float AttackBlock,
    float ElementAttackPower,
    float ElementDefensePower,
    float Critical,
    float CriticalDefence,
    float Luck);

/// <summary>
///     Static data for MyFactor's set-bonus system (report 11 §7): the consolidated coefficient matrix for
///     sets 1-22/30/50/51 (§7.3), the linear per-combine-value ("IU") bonus tables for set-item pieces
///     (§5.7), the NXT tribe set catalog and tier detection (§7.2), and the flat (non-multiplicative)
///     bonuses each set number also grants at various points in GetBase*/Get* (§7.3 footnote, §7.2 table).
///     <see cref="ResolveEffectiveSetNumber" /> is the single entry point <see cref="StatCalculator" /> uses:
///     it tries the fully-specified NXT detection first (matching the legacy's own "NXT checked first, first
///     match wins" order, report §7.1 point 1), and otherwise trusts a caller-supplied legacy set number.
///     Detecting mSetNumber for the legacy sets themselves (1-22, 30, 50, 51, and their 87000-89562 item-id
///     families) requires the exact per-set item-id combinations `CheckSetItemNumber01..15` compare against
///     -- report 11 only gives ID *ranges* and piece *counts* for those, never the full membership lists
///     (§7.1), so that detector is explicitly OUT of this pass's scope: a future dedicated SetDetector
///     component must supply <c>legacySetNumber</c>. This file must not guess those membership lists.
/// </summary>
public static class SetBonusTables
{
    private static readonly FrozenDictionary<int, SetCoefficients> CoefficientsBySetNumber =
        new Dictionary<int, SetCoefficients>
        {
            [1] = new(0f, 0f, 0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f),
            [2] = new(0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [3] = new(0f, 0.6f, 0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f),
            [4] = new(0.6f, 0f, 0f, 0.6f, 0f, 0f, 0f, 0f, 0f),
            [5] = new(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.05f, 0.05f, 0f),
            [6] = new(0f, 0f, 0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f),
            [7] = new(0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [8] = new(0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f),
            [9] = new(0.6f, 0.6f, 0.6f, 0.6f, 0f, 0f, 0.05f, 0f, 0f),
            [10] = new(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.05f, 0.05f, 0.05f),
            [11] = new(0f, 0f, 0f, 0f, 0.6f, 0.6f, 0f, 0f, 0f),
            [12] = new(0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [13] = new(0f, 0.7f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [14] = new(0.7f, 0f, 0f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [15] = new(1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 0f, 0f, 0f),
            [16] = new(0f, 0f, 0f, 0f, 0.6f, 0.6f, 0f, 0f, 0f),
            [17] = new(0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [18] = new(0f, 0f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [19] = new(0.7f, 0.7f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [20] = new(1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 0.05f, 0.05f, 0.10f),
            [21] = new(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 0.05f, 0.05f, 0f),
            [22] = new(0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.04f, 0.04f, 0f),
            // 30 handled by GetSet30Coefficients (slot/legendary-dependent row, not a fixed one).
            [50] = new(0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0f, 0f, 0.05f),
            [51] = new(0f, 0.05f, 0.05f, 0.05f, 0f, 0f, 0.02f, 0.02f, 0f)
        }.ToFrozenDictionary();

    // report §5.7: linear IU->bonus tables for iCheckSetItem==2 pieces. All are 0 at IU=0 and clamp at IU=12
    // except the cape/DefensePower table, which is irregular at IU=12 (520, not the linear 480).
    private static readonly int[] CapeDefenseByCombineTable =
        [0, 40, 80, 120, 160, 200, 240, 280, 320, 360, 400, 440, 520];

    // report §5.3: ReturnWeaponIUSet3Bonus, indexed by IS/Enchant (NOT IU/Combine) for iCheckSetItem==3 weapons.
    private static readonly int[] WeaponIuSet3Table =
        [0, 3500, 4500, 5500, 6500, 7500, 8000, 8500, 9000, 9500, 10000, 10500, 11000];

    // report §7.2: NXT tribe catalogs. Index 0=Noble Dragon, 1=Royal Serpent, 2=Grand Tiger.
    private static readonly int[][] NxtWeaponIdsByTribe =
    [
        [77000, 77001, 77002],
        [77008, 77009, 77010],
        [77016, 77017, 77018]
    ];

    private static readonly int[][] NxtPieceIdsByTribe =
    [
        [77003, 77004, 77005, 77006, 77007],
        [77011, 77012, 77013, 77014, 77015],
        [77019, 77020, 77021, 77022, 77023]
    ];

    /// <summary>
    ///     The coefficient row for <paramref name="setNumber" /> (report §7.3). Unknown/zero set numbers and
    ///     NXT tiers (101-103, which grant only flat bonuses, never a multiplier) resolve to an all-zero row
    ///     via <see cref="FrozenDictionary{TKey,TValue}.GetValueOrDefault(TKey)" />. Set 30 is slot/legendary
    ///     dependent, so it is special-cased rather than stored as one fixed row.
    /// </summary>
    public static SetCoefficients GetCoefficients(int setNumber, int slotIndex, bool isLegendaryItem)
    {
        return setNumber == 30
            ? GetSet30Coefficients(slotIndex, isLegendaryItem)
            : CoefficientsBySetNumber.GetValueOrDefault(setNumber);
    }

    /// <summary>
    ///     Set 30's coefficient row is not fixed: slots 1 (cape) and 10 (deco2) always use 0.55; slots
    ///     9/11/12 (the other decos) contribute 0; every other slot uses 0.55 for a legendary/type-6 item
    ///     (ReturnItemSort ∈ {1,4}) or 1.10 otherwise -- report §7.3 footnote (*). Luck is a flat 0.05
    ///     regardless of slot, matching the footnote's separate LUCK column entry for set 30.
    /// </summary>
    private static SetCoefficients GetSet30Coefficients(int slotIndex, bool isLegendaryItem)
    {
        var factor = slotIndex switch
        {
            1 or 10 => 0.55f,
            9 or 11 or 12 => 0f,
            _ => isLegendaryItem ? 0.55f : 1.10f
        };

        return new SetCoefficients(factor, factor, factor, factor, factor, factor, 0f, 0f, 0.05f);
    }

    /// <summary>Linear IU(1..12)->bonus table shared by every set-item family except the irregular cape/DEF one.</summary>
    public static int LinearByCombine(byte combine, int perUnit)
    {
        var clamped = Math.Min((int)combine, 12);
        return clamped * perUnit;
    }

    /// <summary>ReturnSetItemIUValue_DefensePower(1, IU, set) -- cape DEF table, irregular at IU=12 (report §5.7).</summary>
    public static int CapeDefenseByCombine(byte combine)
    {
        var clamped = Math.Min((int)combine, 12);
        return CapeDefenseByCombineTable[clamped];
    }

    /// <summary>ReturnWeaponIUSet3Bonus(IS) -- weapon iCheckSetItem==3 table, indexed by Enchant/IS (report §5.3).</summary>
    public static int WeaponIuSet3AttackPowerBonus(byte enchant)
    {
        var clamped = Math.Min((int)enchant, 12);
        return WeaponIuSet3Table[clamped];
    }

    /// <summary>
    ///     ReturnCapeIUValue(itemID, value, sort) (report §2): a cape item (world.Items.Sort==29) whose
    ///     Combine/IU tens-digit equals <paramref name="sort" /> grants (units-digit + 1) × <paramref name="perUnit" />
    ///     -- e.g. sort 3 (HP, perUnit 200), sort 1 (ATK, perUnit 50). Returns 0 for any non-cape slot, a cape
    ///     with Combine &lt; 10, or a tens-digit mismatch.
    /// </summary>
    public static int CapeIuBonus(EquippedItemSlot? capeSlot, int sort, float perUnit)
    {
        if (capeSlot is not { } cape) return 0;
        if (cape.Item.Sort != 29) return 0;
        if (cape.Combine < 10) return 0;

        var tens = cape.Combine / 10;
        var units = cape.Combine % 10;
        return tens != sort ? 0 : (int)((units + 1) * perUnit);
    }

    /// <summary>
    ///     Detects the NXT set tier for the 6 canonical slots (index 0=ring, 2=armor, 3=gloves, 4=amulet,
    ///     5=boots per the literal-index convention, 7=weapon) against <paramref name="tribe" />'s catalog
    ///     (report §7.2). Counts, across those 6 slots, how many are occupied by a matching tribe piece (any
    ///     of the 3 weapons in the weapon slot, or any of the 5 non-weapon pieces in the other 5 slots --
    ///     matching CheckSetItemNumberNXT's "sort ids then compare" behavior, which does not require a
    ///     specific piece in a specific one of those 5 slots). Returns 0 (no match), 101 (&gt;=2 pieces), 102
    ///     (&gt;=4), or 103 (&gt;=6, i.e. every canonical slot is a matching NXT piece).
    ///     Report §7.2 notes real avatar data keeps aPreviousTribe in 0..2; tribe values outside that range
    ///     return 0 rather than indexing out of bounds.
    /// </summary>
    public static int DetectNxtSetNumber(byte tribe, IReadOnlyList<EquippedItemSlot> equipment)
    {
        if (tribe > 2) return 0;

        var weaponIds = NxtWeaponIdsByTribe[tribe];
        var pieceIds = NxtPieceIdsByTribe[tribe];
        var matched = 0;

        foreach (var slot in equipment)
        {
            var itemId = slot.Item.ItemId;
            if (slot.SlotIndex == 7)
            {
                if (Array.IndexOf(weaponIds, itemId) >= 0) matched++;
            }
            else if (slot.SlotIndex is 0 or 2 or 3 or 4 or 5)
            {
                if (Array.IndexOf(pieceIds, itemId) >= 0) matched++;
            }
        }

        return matched switch
        {
            >= 6 => 103,
            >= 4 => 102,
            >= 2 => 101,
            _ => 0
        };
    }

    /// <summary>
    ///     Resolves the single "effective set number" every formula below keys off: NXT (101-103) if detected
    ///     (checked first, matching the legacy's own priority order, report §7.1 point 1), otherwise whatever
    ///     legacy set number the caller supplies (0 = none/not yet computed -- see the type's remarks on why
    ///     legacy 1-22/30/50/51 detection is out of this pass's scope).
    /// </summary>
    public static int ResolveEffectiveSetNumber(byte tribe, IReadOnlyList<EquippedItemSlot> equipment,
        int legacySetNumber)
    {
        var nxt = DetectNxtSetNumber(tribe, equipment);
        return nxt != 0 ? nxt : legacySetNumber;
    }

    /// <summary>GetBaseMaxLife flat set bonuses (report §5.1 steps 11-12, §7.3 footnote): all independent, all stack.</summary>
    public static int GetFlatLifeBonus(int setNumber)
    {
        var nxtOrSet20 = setNumber switch
        {
            101 => 1000,
            102 => 2000,
            103 => 3000,
            20 => 20000, // MY_HP else-if branch
            _ => 0
        };

        var pieceBonus = (setNumber == 13 ? 1000 : 0) + (setNumber == 18 ? 1100 : 0);

        // MY_HP "tout set>0 : +15000" -- unconditional on top of everything else above, including set 20 and
        // the NXT tiers (their mSetNumber values, 101-103, are also > 0). Preserved verbatim (D8), not
        // de-duplicated even though it looks redundant with the set-20 branch.
        var anySetBonus = setNumber != 0 ? 15000 : 0;

        return nxtOrSet20 + pieceBonus + anySetBonus;
    }

    /// <summary>GetBaseMaxMana flat set bonuses (report §5.2): no MY_HP-style "any set" bonus for mana.</summary>
    public static int GetFlatManaBonus(int setNumber)
    {
        var nxt = setNumber switch { 101 => 1000, 102 => 2000, 103 => 3000, _ => 0 };
        return nxt + (setNumber == 12 ? 1000 : 0) + (setNumber == 17 ? 1100 : 0);
    }

    /// <summary>GetBaseAttackPower's NXT flat bonus (report §5.3); no other legacy-set flat ATK bonus is documented.</summary>
    public static int GetBaseFlatAttackPowerBonus(int setNumber)
    {
        return setNumber switch { 101 => 500, 102 => 1000, 103 => 1500, _ => 0 };
    }

    /// <summary>GetBaseDefensePower's NXT flat bonus (report §5.4); no other legacy-set flat DEF bonus is documented.</summary>
    public static int GetBaseFlatDefensePowerBonus(int setNumber)
    {
        return setNumber switch { 101 => 1000, 102 => 2000, 103 => 3000, _ => 0 };
    }

    /// <summary>GetAttackSuccess wrapper bonuses (report §6): set 14 flat +100, plus the NXT tier bonus.</summary>
    public static int GetWrapperAttackSuccessBonus(int setNumber)
    {
        var nxt = setNumber switch { 101 => 250, 102 => 500, 103 => 750, _ => 0 };
        return nxt + (setNumber == 14 ? 100 : 0);
    }

    /// <summary>GetAttackBlock wrapper's NXT bonus (report §6); no other set-specific flat DODGE wrapper bonus documented.</summary>
    public static int GetWrapperAttackBlockBonus(int setNumber)
    {
        return setNumber switch { 101 => 250, 102 => 500, 103 => 750, _ => 0 };
    }

    /// <summary>GetElementAttackPower wrapper's set 19 flat bonus (report §6).</summary>
    public static int GetWrapperElementAttackPowerBonus(int setNumber)
    {
        return setNumber == 19 ? 500 : 0;
    }

    /// <summary>GetCritical wrapper's set bonuses (report §6): distinct from GetBaseCritical's own (much smaller) set bonus.</summary>
    public static int GetWrapperCriticalBonus(int setNumber)
    {
        return setNumber switch
        {
            11 or 15 or 16 => 2,
            19 => 5,
            20 or 30 or 50 => 7,
            _ => 0
        };
    }

    /// <summary>GetBaseCritical's own set bonus: only the NXT 6-piece tier (103), report §5.5.</summary>
    public static int GetBaseCriticalFlatBonus(int setNumber)
    {
        return setNumber == 103 ? 1 : 0;
    }

    /// <summary>GetBaseCriticalDefence's set bonuses (report §5.5): distinct set numbers from the wrapper's Critical bonus.</summary>
    public static int GetBaseCriticalDefenceFlatBonus(int setNumber)
    {
        return setNumber switch
        {
            103 => 1,
            15 => 2,
            20 or 30 or 50 => 7,
            _ => 0
        };
    }
}
