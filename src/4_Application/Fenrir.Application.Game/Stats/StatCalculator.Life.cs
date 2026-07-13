using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private const int LifeElixirRate = 20;
    private const int ManaElixirRate = 25;
    private const int StrengthElixirAttackRate = 3;

    private const int ElementElixirRate = 10;

    // Boost-potion / warrior-pill derived-stat multipliers, and the single zone in which they are suppressed
    // for both life and attack. MyFactor.cpp:1941-1943 (life x1.2), :2610-2612 (attack x1.1).
    private const short BoostExcludedZoneNumber = 124;
    private const float LifeBoostMultiplier = 1.2f;

    private static int ComputeMaxLife(int vitality, LevelRowDto levelRow, int setNumber, bool isLegendarySet,
        byte previousTribe, EquippedItemSlot?[] bySlot, int petLife,
        ZoneContext zone = default, ConsumableContext consumable = default, MountContext mount = default,
        CosmeticContext cosmetic = default)
    {
        var hp = (int)(vitality * 20.0f);
        hp += levelRow.Life;

        // Legacy folds ornament + sort-2 decoration + zone elixir into the base+level subtotal BEFORE the boost
        // multiplier and the pet-doubling cliff, since any of them can push the subtotal across the pet-life
        // threshold -- this is the load-bearing accumulation order the cliff depends on. MyFactor.cpp:1922-1943.
        hp += OrnamentLifeContribution(zone, bySlot);
        hp += DecorationStatContribution(DecorationStatKind.MaxLife, bySlot);
        hp += LifeElixirContributionWithOverride(consumable, zone);

        // Boost-potion / warrior-pill life multiplier (x1.2): after the level factor, before the mount-grade
        // multiplier, suppressed in zone 124. OR-logic across the two sources, applied exactly once.
        // MyFactor.cpp:1941-1943.
        hp = ApplyLifeBoostMultiplier(hp, consumable, zone);

        // Ridden-mount grade multiplier (four-tier, HP marker) applied to the level/attribute subtotal, before
        // the later equipment/set additions; the flat rolled bonus is added at the end (after the multiply).
        hp = MountGradeMaxLife(hp, mount);

        // Pet-doubling cliff, evaluated on the fully-accumulated subtotal (base + level + ornament + deco +
        // elixir + x1.2 boost + mount grade) so the ">= tPetLife" threshold matches legacy. MyFactor.cpp:1973-1977.
        hp = ApplyPetDoubleRule(hp, petLife);

        hp += SetBonusTables.GetFlatLifeBonus(setNumber);
        hp += ComputeG12CustomSetBonus(previousTribe, bySlot);
        if (isLegendarySet) hp += 30000;

        if (bySlot[0] is { } amulet)
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
        {
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 5000, 7500, 12500);
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

            if (!PetAmuletPhoenixOverlapIds.Contains(petAmulet.Item.ItemId))
                hp += PetAmuletLifeBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);
        }

        hp += StellarCoreMaxLifeContribution(cosmetic);
        hp += RankBuffMaxLifeBonus(zone);

        hp = ApplyDrunkMaxLife(hp, zone);

        hp += MountFlatMaxLife(mount);

        return ApplyFreeForAllMaxLife(hp, zone.ZoneNumber);
    }

    // Life boost multiplier: x1.2 when active (HP-boost potion OR warrior pill) and outside the excluded zone.
    // OR-logic, never additive -- two active sources still yield a single x1.2, never x1.2 squared. The single-
    // precision multiply then truncate-toward-zero matches the legacy (int)((float)value * 1.2f).
    private static int ApplyLifeBoostMultiplier(int hp, ConsumableContext consumable, ZoneContext zone)
    {
        return zone.ZoneNumber != BoostExcludedZoneNumber &&
               (consumable.HpBoostActive || consumable.WarriorPillActive)
            ? (int)(hp * LifeBoostMultiplier)
            : hp;
    }

    private static int ComputeG12CustomSetBonus(byte previousTribe, EquippedItemSlot?[] bySlot)
    {
        var range = previousTribe switch
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
            if (d is >= 1 and <= 5 && u is >= 1 and <= 5) total += d * 1000;
        }

        return total;
    }

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

        return (v5 >= 6 ? 5000 : 0) + (v7 >= 6 ? 15000 : 0);
    }


    private static int ComputeMaxMana(int ki, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        int petMana, ZoneContext zone = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var mp = (int)(ki * 15.3100004196167f);
        mp += levelRow.Mana;

        // Mana receives NO boost multiplier, but the same accumulation-then-cliff ordering as life applies:
        // ornament + sort-2 decoration + zone elixir fold into the base+level subtotal before the mount-grade
        // multiplier and the pet-doubling cliff. MyFactor.cpp:2242-2284.
        mp += OrnamentManaContribution(zone, bySlot);
        mp += DecorationStatContribution(DecorationStatKind.MaxMana, bySlot);
        mp += ManaElixirContributionWithOverride(consumable, zone);

        // Ridden-mount grade multiplier (four-tier, MP marker), flat rolled bonus added at the end.
        mp = MountGradeMaxMana(mp, mount);

        // Pet-doubling cliff on the fully-accumulated subtotal (base + level + ornament + deco + elixir +
        // mount grade) so the ">= tPetMana" threshold matches legacy. MyFactor.cpp:2280-2284.
        mp = ApplyPetDoubleRule(mp, petMana);

        mp += SetBonusTables.GetFlatManaBonus(setNumber);
        mp += SetBonusTables.CapeIuBonus(bySlot[1], 4, 250f);

        if (bySlot[1] is { } cape)
            mp += cape.Item.ItemId switch { 1401 => 50, 1404 => 100, _ => 0 };

        if (bySlot[8] is { } petAmulet)
        {
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 5000, 7500, 12500);
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

            if (!PetAmuletPhoenixOverlapIds.Contains(petAmulet.Item.ItemId))
                mp += PetAmuletManaBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);
        }

        mp += MountFlatMaxMana(mount);

        return ApplyFreeForAllMaxMana(mp, zone.ZoneNumber);
    }

    private static bool IsElixirEligibleZone(short zoneNumber)
    {
        if (zoneNumber is >= 319 and <= 323)
            return true;
        return !IsElixirSuppressedZone(zoneNumber);
    }

    private static bool IsElixirSuppressedZone(short zoneNumber)
    {
        return false;
    }

    private static bool IsElementElixirZoneAllowed(short zoneNumber)
    {
        return IsElixirEligibleZone(zoneNumber) && zoneNumber != 84;
    }

    private static int DecodeElementAttackCount(int packedElePotion)
    {
        return packedElePotion / 1000;
    }

    private static int DecodeElementDefenseCount(int packedElePotion)
    {
        return packedElePotion % 1000;
    }


    private static int ElementAttackElixirContribution(ConsumableContext consumable, ZoneContext zone)
    {
        return IsElementElixirZoneAllowed(zone.ZoneNumber)
            ? ElementElixirRate * DecodeElementAttackCount(consumable.EatElePotion)
            : 0;
    }

    private static int ElementDefenseElixirContribution(ConsumableContext consumable, ZoneContext zone)
    {
        return IsElementElixirZoneAllowed(zone.ZoneNumber)
            ? ElementElixirRate * DecodeElementDefenseCount(consumable.EatElePotion)
            : 0;
    }
}
