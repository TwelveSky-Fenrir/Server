namespace Fenrir.Application.Game.Stats;

// Legacy equipment-set classifier (MyUtil::ReturnSetItemValue, EQUIP_INFO4* variant, codes 21/22 for the
// God full-six sets). Produces the single mSetNumber consumed downstream by SetBonusTables. NXT class sets
// (101/102/103) are the highest-priority rule but are resolved one level up in
// SetBonusTables.ResolveEffectiveSetNumber, so this method covers only the non-NXT tail (codes 1-22, 30, 50, 0)
// in the legacy priority order. The raw id combination tables live in StatCalculator.SetDetectorTables.cs.
// Ref: Server/ts25zone/S07_MyGame03.cpp:7628-7768 (priority order, -129 elite re-check, God ring/amulet
// override, 50/20/30/0 fallback).
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

        // Rule 2: rare sets 1-8 (base tier, no id offset).
        var rare = DetectRareSetNumber(bySlot, 0, 0);
        if (rare != 0) return rare;

        // Rule 3: legendary four-body set 9 (God ring/amulet forces 0 and stops; never falls through).
        if (MatchesAnyCombination(bySlot, FourBodySlots, Set09Combinations))
            return HasGodRingOrAmulet(bySlot) ? 0 : 9;

        // Rule 4: full-six sets, in legacy dispatch order (10, 21, 22, 15).
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set10Combinations)) return 10;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set21Combinations)) return 21;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set22Combinations)) return 22;
        if (MatchesAnyCombination(bySlot, SixGearSlots, Set15Combinations)) return 15;

        // Rule 5: elite tier of rare sets 1-8 (each checked id reduced by 129, re-tested against Set01-08),
        // returned as codes 11-18. Code 15 is reachable here (elite Set05) as well as via rule 4 (Set15).
        var elite = DetectRareSetNumber(bySlot, -EliteIdOffset, 10);
        if (elite != 0) return elite;

        // Rule 6: legendary four-body set 19 (original ids, same God ring/amulet override).
        if (MatchesAnyCombination(bySlot, FourBodySlots, Set19Combinations))
            return HasGodRingOrAmulet(bySlot) ? 0 : 19;

        // Rules 7-8: mixed-count fallback (50/20/30) or no set (0).
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

    // Order-independent exact-combination match: every checked slot must be equipped, and once the checked
    // slot ids are (offset-adjusted and) sorted ascending they must equal one enumerated combination exactly.
    // This reproduces the legacy multiset test (each live CheckSetItemNumberNN sorts its arguments before
    // comparing against one concrete tuple) rather than the looser flat per-id membership the earlier C# used,
    // which would have accepted cross-group id mixes legacy rejects.
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
