namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseMaxLife ----

    private static int ComputeMaxLife(int vitality, LevelRowDto levelRow, int setNumber, bool isLegendarySet,
        byte tribe, EquippedItemSlot?[] bySlot, int petLife)
    {
        var hp = (int)(vitality * 20.0f);
        hp += levelRow.Life; // ornament/deco/elixir bonuses unmodeled, skipped
        hp = ApplyPetDoubleRule(hp, petLife); // HP-boost-pill/animal-grade/mount bonuses unmodeled

        hp += SetBonusTables.GetFlatLifeBonus(setNumber);
        hp += ComputeG12CustomSetBonus(tribe, bySlot);
        if (isLegendarySet) hp += 30000;

        if (bySlot[0] is { } amulet) // slot EAMULET(0) -- literal index, see naming-inversion note on EquippedItemSlot
        {
            var enchant = (int)amulet.Enchant;
            if (enchant > 0)
            {
                if (enchant > 100) enchant -= 100;
                hp += 500 * enchant;
            }
        }

        hp += ComputeIsIuForLifeBonus(bySlot);
        hp += ComputeG12LifeUpBonus(bySlot);
        hp += SetBonusTables.CapeIuBonus(bySlot[1], 3, 200f);

        if (bySlot[8] is { } petAmulet)
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

        return hp;
    }

    /// <summary>G12 custom-set bonus, gated on all 6 canonical slots being in-tribe IDs.</summary>
    private static int ComputeG12CustomSetBonus(byte tribe, EquippedItemSlot?[] bySlot)
    {
        var range = tribe switch
        {
            0 => (Lo: 84500, Hi: 84699),
            1 => (Lo: 85500, Hi: 85699),
            2 => (Lo: 86500, Hi: 86699),
            _ => (Lo: 0, Hi: -1)
        };
        if (range.Hi < range.Lo) return 0;

        int[] relevantSlots = [0, 2, 3, 4, 5, 7];
        var minCombine = -1;
        foreach (var slotIndex in relevantSlots)
        {
            if (bySlot[slotIndex] is not { } slot) return 0;
            var id = slot.Item.ItemId;
            if (id < range.Lo || id > range.Hi) return 0;
            if (minCombine < 0 || slot.Combine < minCombine) minCombine = slot.Combine;
        }

        return minCombine switch { >= 12 => 15000, >= 6 => 5000, _ => 0 };
    }

    /// <summary>sort==1 items at slots 0-7 except cape(1)/null(6) -- specifically sort==1, not the usual {1,4} legendary set.</summary>
    private static int ComputeIsIuForLifeBonus(EquippedItemSlot?[] bySlot)
    {
        var total = 0;
        for (var i = 0; i <= 7; i++)
        {
            if (i is 1 or 6) continue;
            if (bySlot[i] is not { } slot) continue;
            if (slot.Item.Sort != 1)
                continue;

            var d = slot.Combine / 10;
            var u = slot.Combine % 10;
            // Confirmed range D in 1..5, U in 1..5 -- not extrapolated beyond that.
            if (d is >= 1 and <= 5 && u is >= 1 and <= 5) total += d * 1000;
        }

        return total;
    }

    /// <summary>ReturnSetItemIUValue_LifeUp: G12 (MartialLevelLimit==12) non-legendary pieces.</summary>
    private static int ComputeG12LifeUpBonus(EquippedItemSlot?[] bySlot)
    {
        var v5 = 0;
        var v7 = 0;
        for (var i = 0; i <= 7; i++)
        {
            if (i is 1 or 6) continue;
            if (bySlot[i] is not { } slot) continue;
            if (slot.Item.MartialLevelLimit != 12) continue;
            if (IsLegendary(slot.Item)) continue;
            if (slot.Combine >= 6) v5++;
            if (slot.Combine >= 12) v7++;
        }

        // v5>=6 -> +5000; v7>=6 -> +15000 more (6 CS12 pieces = +20000 total).
        return (v5 >= 6 ? 5000 : 0) + (v7 >= 6 ? 15000 : 0);
    }

    // ---- GetBaseMaxMana ----

    private static int ComputeMaxMana(int ki, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        int petMana)
    {
        var mp = (int)(ki * 15.3100004196167f); // exact source literal, not a rounded 15.31f
        mp += levelRow.Mana;
        mp = ApplyPetDoubleRule(mp, petMana);

        mp += SetBonusTables.GetFlatManaBonus(setNumber);
        mp += SetBonusTables.CapeIuBonus(bySlot[1], 4, 250f);

        if (bySlot[1] is { } cape)
            mp += cape.Item.ItemId switch { 1401 => 50, 1404 => 100, _ => 0 };

        if (bySlot[8] is { } petAmulet)
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

        return mp;
    }
}
