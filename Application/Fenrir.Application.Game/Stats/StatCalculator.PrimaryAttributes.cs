namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // The 5 title-rank vectors (rank 1..14), reused across the 4 base stats' own tranche tables.
    private static readonly int[] TitleTableA = [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 67, 82, 97, 112];
    private static readonly int[] TitleTableB = [0, 1, 3, 5, 8, 11, 14, 18, 23, 28, 34, 41, 48, 55];
    private static readonly int[] TitleTableC = [2, 6, 12, 20, 30, 42, 56, 72, 90, 110, 134, 164, 194, 224];
    private static readonly int[] TitleTableD = [1, 2, 3, 5, 7, 10, 14, 18, 22, 27, 33, 41, 49, 57];
    private static readonly int[] TitleTableE = [3, 8, 15, 25, 37, 52, 70, 90, 112, 137, 167, 205, 243, 281];

    // ---- GetBaseVitality/Strength/Ki(Intelligence)/Wisdom(Dexterity) ----

    /// <summary>
    ///     stat = aHalo + rawStat + sum(13 slots) item stat + titleBonus. The aHalo term is not a typo --
    ///     it's added here on top of CriticalDefence's own separate halo/10 bonus; both are real, independent
    ///     uses of the same field.
    /// </summary>
    private static int ComputeVitality(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Vitality;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Vitality;
        return total + TitleVitalityBonus(attributes.Title);
    }

    private static int ComputeStrength(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Strength;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Strength;
        return total + TitleStrengthBonus(attributes.Title);
    }

    private static int ComputeKi(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Intelligence;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Intelligent;
        return total + TitleKiBonus(attributes.Title);
    }

    private static int ComputeWisdom(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Dexterity;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Dexterity;
        return total + TitleWisdomBonus(attributes.Title);
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
