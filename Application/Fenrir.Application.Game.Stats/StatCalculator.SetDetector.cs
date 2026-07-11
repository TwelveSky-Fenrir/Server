using System.Collections.Frozen;

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

    private static readonly FrozenSet<int>
        Set01Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set02Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set03Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set04Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set05Ids = FrozenSet<int>
            .Empty;

    private static readonly FrozenSet<int>
        Set06Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set07Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set08Ids = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set09LegendaryIds =
            FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set10FullSixIds = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set21GodFullSixIds =
            FrozenSet<int>
                .Empty;

    private static readonly FrozenSet<int>
        Set22GodFullSixIds =
            FrozenSet<int>
                .Empty;

    private static readonly FrozenSet<int>
        Set15FullSixIds =
            FrozenSet<int>.Empty;

    private static readonly FrozenSet<int>
        Set19LegendaryIds = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int> GodRingAmuletIds = FrozenSet<int>.Empty;

    private static readonly FrozenSet<int> LegendaryPieceIds = FrozenSet<int>.Empty;

    public static int DetectLegacySetNumber(IReadOnlyList<EquippedItemSlot> equipment)
    {
        var bySlot = BuildSlotLookup(equipment);

        var rare = DetectRareSetNumber(bySlot, 0, 0);
        if (rare != 0) return rare;


        if (AllSlotIdsMatch(bySlot, FourBodySlots, Set09LegendaryIds))
            return HasGodRingOrAmulet(bySlot) ? 0 : 9;

        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set10FullSixIds)) return 10;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set21GodFullSixIds)) return 21;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set22GodFullSixIds)) return 22;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set15FullSixIds)) return 15;

        var elite = DetectRareSetNumber(bySlot, -EliteIdOffset, 10);
        if (elite != 0) return elite;

        if (AllSlotIdsMatch(bySlot, FourBodySlots, Set19LegendaryIds))
            return HasGodRingOrAmulet(bySlot) ? 0 : 19;

        return MixedCountFallback(bySlot);
    }

    private static int DetectRareSetNumber(EquippedItemSlot?[] bySlot, int idOffset, int resultBase)
    {
        if (AllSlotIdsMatch(bySlot, RingAmuletSlots, Set01Ids, idOffset)) return resultBase + 1;
        if (AllSlotIdsMatch(bySlot, WeaponArmorSlots, Set02Ids, idOffset)) return resultBase + 2;
        if (AllSlotIdsMatch(bySlot, GlovesBootsArmorSlots, Set03Ids, idOffset)) return resultBase + 3;
        if (AllSlotIdsMatch(bySlot, WeaponBootsSlots, Set04Ids, idOffset)) return resultBase + 4;
        if (AllSlotIdsMatch(bySlot, SixGearSlots, Set05Ids, idOffset)) return resultBase + 5;
        if (AllSlotIdsMatch(bySlot, RingAmuletSlots, Set06Ids, idOffset)) return resultBase + 6;
        if (AllSlotIdsMatch(bySlot, WeaponArmorSlots, Set07Ids, idOffset)) return resultBase + 7;
        if (AllSlotIdsMatch(bySlot, GlovesBootsSlots, Set08Ids, idOffset)) return resultBase + 8;
        return 0;
    }

    private static bool AllSlotIdsMatch(EquippedItemSlot?[] bySlot, ReadOnlySpan<int> slotIndices,
        FrozenSet<int> ids, int idOffset = 0)
    {
        if (ids.Count == 0) return false;
        foreach (var slotIndex in slotIndices)
        {
            if (bySlot[slotIndex] is not { } slot) return false;
            if (!ids.Contains(slot.Item.ItemId + idOffset)) return false;
        }

        return true;
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
        var sortCount = 0;
        foreach (var slotIndex in SixGearSlots)
        {
            if (bySlot[slotIndex] is not { } slot) continue;
            if (LegendaryPieceIds.Contains(slot.Item.ItemId)) legendaryPieceCount++;
            if (IsLegendary(slot.Item)) sortCount++;
        }

        if (sortCount == 6) return 50;
        if (legendaryPieceCount == 6) return 20;
        if (legendaryPieceCount + sortCount == 6) return 30;
        return 0;
    }
}
