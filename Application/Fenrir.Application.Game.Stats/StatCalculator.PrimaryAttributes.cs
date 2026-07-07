namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // The 5 title-rank vectors (rank 1..14), reused across the 4 base stats' own tranche tables.
    private static readonly int[] TitleTableA = [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 67, 82, 97, 112];
    private static readonly int[] TitleTableB = [0, 1, 3, 5, 8, 11, 14, 18, 23, 28, 34, 41, 48, 55];
    private static readonly int[] TitleTableC = [2, 6, 12, 20, 30, 42, 56, 72, 90, 110, 134, 164, 194, 224];
    private static readonly int[] TitleTableD = [1, 2, 3, 5, 7, 10, 14, 18, 22, 27, 33, 41, 49, 57];
    private static readonly int[] TitleTableE = [3, 8, 15, 25, 37, 52, 70, 90, 112, 137, 167, 205, 243, 281];

    // ---- Custom-decoration-item primary-stat conversion bonus ----
    // Server/Header/Protocol/MyFactor.cpp:150-273 (MyFactor_IsCustomDecoStatItem + the 4 sibling
    // MyFactor_GetCustomDeco{Str,Vit,Int,Dex}Bonus helpers); unconditional tail calls at
    // GetBaseVitality:1710, GetBaseKi:1769, GetBaseStrength:1828, GetBaseWisdom:1887. Additive on top of
    // the ordinary per-slot item-stat sum above (both read the same 4 decoration slots) -- not a
    // replacement for it: a qualifying item's own low raw stat is counted once via the sum above, then a
    // second time here as (100 - rawStat).

    /// <summary>Item ids 594-596/1385/1389/1393/1483-1485/2307-2309/8010-8012/91483-91488 (21 ids total).</summary>
    private static readonly HashSet<int> CustomDecoStatItemExplicitIds =
    [
        594, 595, 596,
        1385, 1389, 1393,
        1483, 1484, 1485,
        2307, 2308, 2309,
        8010, 8011, 8012,
        91483, 91484, 91485, 91486, 91487, 91488
    ];

    // ---- GetBaseVitality/Strength/Ki(Intelligence)/Wisdom(Dexterity) ----

    /// <summary>
    ///     stat = aHalo + rawStat + sum(13 slots) item stat + titleBonus + customDecoBonus. The aHalo term is
    ///     not a typo -- it's added here on top of CriticalDefence's own separate halo/10 bonus; both are
    ///     real, independent uses of the same field.
    /// </summary>
    private static int ComputeVitality(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Vitality;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Vitality;
        return total + TitleVitalityBonus(attributes.Title) + CustomDecoVitBonus(bySlot);
    }

    private static int ComputeStrength(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Strength;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Strength;
        return total + TitleStrengthBonus(attributes.Title) + CustomDecoStrBonus(bySlot);
    }

    private static int ComputeKi(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Intelligence;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Intelligent;
        return total + TitleKiBonus(attributes.Title) + CustomDecoIntBonus(bySlot);
    }

    private static int ComputeWisdom(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Dexterity;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Dexterity;
        return total + TitleWisdomBonus(attributes.Title) + CustomDecoDexBonus(bySlot);
    }

    private static bool IsCustomDecoStatItem(int itemId)
    {
        return itemId is >= 101 and <= 151 || CustomDecoStatItemExplicitIds.Contains(itemId);
    }

    /// <summary>
    ///     Decoration slots are the raw equipment-array indices 9-12 (EDECO1-EDECO4, STRUCT.h:1662-1676).
    ///     Each qualifying item whose own raw stat is below 100 contributes (100 - rawStat); an empty slot or
    ///     a non-qualifying item silently contributes 0.
    /// </summary>
    private static int CustomDecoVitBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Vitality < 100)
                bonus += 100 - s.Item.Vitality;
        return bonus;
    }

    private static int CustomDecoStrBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Strength < 100)
                bonus += 100 - s.Item.Strength;
        return bonus;
    }

    private static int CustomDecoIntBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Intelligent < 100)
                bonus += 100 - s.Item.Intelligent;
        return bonus;
    }

    private static int CustomDecoDexBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Dexterity < 100)
                bonus += 100 - s.Item.Dexterity;
        return bonus;
    }

    // ---- Title-rank bonus tranches (deliberately non-uniform if-nesting per stat) ----

    private static int TitleRankBonus(int[] table, int rank)
    {
        return rank is >= 1 and <= 14 ? table[rank - 1] : 0;
    }

    private static int TitleVitalityBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        // No <=200 test (101-300 both map to B) -- confirmed gap, not an omission.
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableC,
            _ => TitleTableB
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleStrengthBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableC,
            <= 300 => TitleTableD,
            <= 400 => TitleTableB,
            _ => TitleTableA
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleKiBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableA,
            _ => TitleTableC
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleWisdomBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        // No <=400 test (301-400 and >400 both map to D) -- confirmed gap, not an omission.
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableD,
            <= 300 => TitleTableE,
            _ => TitleTableD
        };
        return TitleRankBonus(table, rank);
    }
}
