namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private const int EliteIdOffset = 129;

    private static readonly int[] SixGearSlots = [0, 2, 3, 4, 5, 7];
    private static readonly int[] RingAmuletSlots = [4, 0];
    private static readonly int[] WeaponArmorSlots = [7, 2];
    private static readonly int[] GlovesBootsArmorSlots = [3, 5, 2];
    private static readonly int[] GlovesBootsSlots = [3, 5];
    private static readonly int[] WeaponBootsSlots = [7, 5];
    private static readonly int[] FourBodySlots = [7, 2, 3, 5];

    public static int DetectLegacySetNumber(IReadOnlyList<EquippedItemSlot> equipment)
    {
        var bySlot = BuildSlotLookup(equipment);

        var rare = DetectRareSetNumber(bySlot, 0, 0);
        if (rare != 0) return rare;

        if (MatchesAnyCombination(bySlot, FourBodySlots, Set09Combinations))
            return HasGodRingOrAmulet(bySlot) ? 0 : 9;

        if (MatchesAnyCombination(bySlot, SixGearSlots, Set10Combinations)) return 10;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set21Combinations)) return 21;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set22Combinations)) return 22;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set15Combinations)) return 15;

        var elite = DetectRareSetNumber(bySlot, -EliteIdOffset, 10);
        if (elite != 0) return elite;

        if (MatchesAnyCombination(bySlot, FourBodySlots, Set19Combinations))
            return HasGodRingOrAmulet(bySlot) ? 0 : 19;

        return MixedCountFallback(bySlot);
    }

    private static int DetectRareSetNumber(EquippedItemSlot?[] bySlot, int idOffset, int resultBase)
    {
        if (MatchesAnyCombination(bySlot, RingAmuletSlots, Set01Combinations, idOffset)) return resultBase + 1;
        if (MatchesAnyCombination(bySlot, WeaponArmorSlots, Set02Combinations, idOffset)) return resultBase + 2;
        if (MatchesAnyCombination(bySlot, GlovesBootsArmorSlots, Set03Combinations, idOffset)) return resultBase + 3;
        if (MatchesAnyCombination(bySlot, WeaponBootsSlots, Set04Combinations, idOffset)) return resultBase + 4;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set05Combinations, idOffset)) return resultBase + 5;
        if (MatchesAnyCombination(bySlot, RingAmuletSlots, Set06Combinations, idOffset)) return resultBase + 6;
        if (MatchesAnyCombination(bySlot, WeaponArmorSlots, Set07Combinations, idOffset)) return resultBase + 7;
        if (MatchesAnyCombination(bySlot, GlovesBootsSlots, Set08Combinations, idOffset)) return resultBase + 8;
        return 0;
    }

    private static bool MatchesAnyCombination(EquippedItemSlot?[] bySlot, ReadOnlySpan<int> slotIndices,
        int[][] combinations, int idOffset = 0)
    {
        if (combinations.Length == 0) return false;

        Span<int> ids = stackalloc int[slotIndices.Length];
        for (var i = 0; i < slotIndices.Length; i++)
        {
            if (bySlot[slotIndices[i]] is not { } slot) return false;
            ids[i] = slot.Item.ItemId + idOffset;
        }

        ids.Sort();
        foreach (var combination in combinations)
            if (ids.SequenceEqual(combination))
                return true;

        return false;
    }

    private static bool HasGodRingOrAmulet(EquippedItemSlot?[] bySlot)
    {
        if (GodRingAmuletIds.Count == 0) return false;
        if (bySlot[4] is { } ring && GodRingAmuletIds.Contains(ring.Item.ItemId)) return true;
        if (bySlot[0] is { } amulet && GodRingAmuletIds.Contains(amulet.Item.ItemId)) return true;
        return false;
    }

    private static int MixedCountFallback(EquippedItemSlot?[] bySlot)
    {
        var legendaryPieceCount = 0;
        var legendaryTierCount = 0;
        foreach (var slotIndex in SixGearSlots)
        {
            if (bySlot[slotIndex] is not { } slot) continue;
            if (LegendaryPieceIds.Contains(slot.Item.ItemId)) legendaryPieceCount++;
            if (IsLegendary(slot.Item)) legendaryTierCount++;
        }

        if (legendaryTierCount == 6) return 50;
        if (legendaryPieceCount == 6) return 20;
        if (legendaryPieceCount + legendaryTierCount == 6) return 30;
        return 0;
    }
}
