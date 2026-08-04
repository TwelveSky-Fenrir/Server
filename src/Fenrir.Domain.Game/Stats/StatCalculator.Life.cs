using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private const int LifeElixirRate = 20;
    private const int ManaElixirRate = 25;
    private const int StrengthElixirAttackRate = 3;

    private const int ElementElixirRate = 10;

    private const short BoostExcludedZoneNumber = 124;
    private const float LifeBoostMultiplier = 1.2f;

    private static readonly FrozenSet<short> LevelBattleZoneNumbers = ((short[])
    [
        49, 51, 53, 120, 121, 122,
        146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164,
        295, 296, 319, 320, 321, 322, 323
    ]).ToFrozenSet();

    private static int ComputeMaxLife(int vitality, LevelRowDto levelRow, int setNumber, bool isLegendarySet,
        byte avatarTribe, byte previousTribe, EquippedItemSlot?[] bySlot, int petLife,
        ZoneContext zone = default, ConsumableContext consumable = default, MountContext mount = default,
        CosmeticContext cosmetic = default)
    {
        var hp = (int)(vitality * 20.0f);
        hp += levelRow.Life;

        hp += OrnamentLifeContribution(zone, bySlot);
        hp += DecorationStatContribution(DecorationStatKind.MaxLife, bySlot);
        hp += LifeElixirContributionWithOverride(consumable, zone, avatarTribe: avatarTribe,
            eventTribe: consumable.EventTribe);

        hp = ApplyLifeBoostMultiplier(hp, consumable, zone);

        hp = MountGradeMaxLife(hp, mount);

        hp = ApplyPetDoubleRule(hp, petLife);

        hp += SetBonusTables.GetFlatLifeBonus(setNumber);
        hp += ComputeG12CustomSetBonus(previousTribe, bySlot);
        if (isLegendarySet) hp += 30000;

        if (bySlot[0] is { } amulet && IsLegendary(amulet.Item))
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
            hp += PetAmuletLifeBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);

        hp += StellarCoreMaxLifeContribution(cosmetic);
        hp += RankBuffMaxLifeBonus(zone);

        hp = ApplyDrunkMaxLife(hp, zone);

        hp += MountFlatMaxLife(mount);

        return hp;
    }

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
            if (ItemSortClass(slot.Item) != 1)
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

        return (v5 >= 4 ? 5000 : 0) + (v7 >= 4 ? 15000 : 0);
    }


    private static int ComputeMaxMana(int ki, LevelRowDto levelRow, int setNumber, byte avatarTribe,
        EquippedItemSlot?[] bySlot, int petMana, ZoneContext zone = default, ConsumableContext consumable = default,
        MountContext mount = default)
    {
        var mp = (int)(ki * 15.3100004196167f);
        mp += levelRow.Mana;

        mp += OrnamentManaContribution(zone, bySlot);
        mp += DecorationStatContribution(DecorationStatKind.MaxMana, bySlot);
        mp += ManaElixirContributionWithOverride(consumable, zone, avatarTribe: avatarTribe,
            eventTribe: consumable.EventTribe);

        mp = MountGradeMaxMana(mp, mount);

        mp = ApplyPetDoubleRule(mp, petMana);

        mp += SetBonusTables.GetFlatManaBonus(setNumber);
        mp += SetBonusTables.CapeIuBonus(bySlot[1], 4, 250f);

        if (bySlot[1] is { } cape)
            mp += cape.Item.ItemId switch { 1401 => 50, 1404 => 100, _ => 0 };

        if (bySlot[8] is { } petAmulet)
            mp += PetAmuletManaBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);

        mp += MountFlatMaxMana(mount);

        return mp;
    }

    private static bool IsElixirEligibleZone(short zoneNumber)
    {
        if (zoneNumber is >= 319 and <= 323)
            return true;
        return !IsElixirSuppressedZone(zoneNumber);
    }

    private static bool IsElixirSuppressedZone(short zoneNumber)
    {
        return LevelBattleZoneNumbers.Contains(zoneNumber) || zoneNumber == 250 || zoneNumber is > 266 and <= 269;
    }

    private static bool IsElementElixirZoneAllowed(short zoneNumber)
    {
        if (!IsElixirEligibleZone(zoneNumber))
            return false;

        return (zoneNumber != 84 && zoneNumber < 235) || zoneNumber is > 249 and < 292 || zoneNumber > 294;
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
