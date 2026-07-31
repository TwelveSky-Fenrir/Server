using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private const float AttackBoostMultiplier = 1.1f;

    private static int ComputeAttackPower(int strength, int ki, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot, CosmeticContext cosmetic = default, ZoneContext zone = default,
        MountContext mount = default, ConsumableContext consumable = default,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
    {
        var weaponSlot = bySlot[7];
        var coefficients = ResolveWeaponAttackCoefficients(weaponSlot);
        var atk = (int)(strength * coefficients.Str) + (int)(ki * coefficients.Ki);
        atk += levelRow.AttackPower;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            atk += slot.Item.AttackPower;
            if (i != 8)
                atk += (int)(slot.Item.AttackPower *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackPower);
            if (gemSocketsByTypeAndValue is not null)
                atk += SumGemSocketContribution(GemSocketStatKind.AttackPower, slot.SocketGem1, slot.SocketGem2,
                    slot.SocketGem3, gemSocketsByTypeAndValue);
        }

        atk = MountGradeAttack(atk, mount);

        if (bySlot[1] is { } capeSlot)
        {
            if (capeSlot.Item.Sort == 29) atk += 6 * capeSlot.Enchant;
            atk += capeSlot.Item.ItemId switch { 1404 => 1100, 1401 => 100, _ => 0 };
        }

        atk += ComputeWeaponAttackPowerBonus(weaponSlot);

        if (bySlot[10] is { } deco2)
            atk += ComputeDeco2AttackPowerBonus(deco2);

        if (bySlot[8] is { } petAmulet)
        {
            atk -= petAmulet.Item
                .AttackPower;
            atk += PhoenixFlatBonus(petAmulet.Item.ItemId, 3000, 4000, 5000);
            atk += PhoenixDamageSecondPassBonus(petAmulet.Item.ItemId);

            if (!PetAmuletPhoenixOverlapIds.Contains(petAmulet.Item.ItemId))
                atk += PetAmuletAttackBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);
        }

        atk += SetBonusTables.GetBaseFlatAttackPowerBonus(setNumber);

        atk += StrengthElixirAttackContributionWithOverride(consumable, zone);

        atk = ApplyAttackBoostMultiplier(atk, consumable, zone);

        atk += StellarCoreAttackPowerContribution(cosmetic);
        atk += OrnamentAttackContribution(zone, bySlot);

        atk += MountFlatAttack(mount);

        return atk;
    }

    private static (float Str, float Ki) ResolveWeaponAttackCoefficients(EquippedItemSlot? weapon)
    {
        return (weapon?.Item.Sort ?? 0) switch
        {
            13 or 17 or 19 => (3.65f, 2.43f),
            14 or 16 or 20 => (3.80f, 2.51f),
            15 or 18 or 21 => (3.51f, 2.35f),
            _ => (2.25f, 1.67f)
        };
    }

    private static int ComputeWeaponAttackPowerBonus(EquippedItemSlot? weaponSlot)
    {
        if (weaponSlot is not { } weapon) return 0;
        var item = weapon.Item;
        var total = 0;

        if (IsLegendary(item))
        {
            var enchant = (int)weapon.Enchant;
            if (enchant >= 100)
                enchant -= 100;
            total += enchant * 1200;
        }

        if (item.CheckSetItem == 2)
        {
            total += SetBonusTables.LinearByCombine(weapon.Combine, 400);
            var enchant = weapon.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)(item.AttackPower * enchant * 0.03f);
        }
        else if (item.CheckSetItem == 3)
        {
            total += SetBonusTables.WeaponIuSet3AttackPowerBonus(weapon.Enchant);
        }
        else
        {
            var effect = WeaponAttackEffectValue(item);
            var e = effect * weapon.Combine;
            total += e;
            var enchant = weapon.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)((e + item.AttackPower) * enchant * 0.03f);
        }

        return total;
    }

    private static int WeaponAttackEffectValue(ItemRowDto weapon)
    {
        if (weapon.Sort != 4 && weapon.Sort is < 13 or > 21) return 0;

        var level = (float)weapon.Level;
        var (baseValue, pivot, slope) = level switch
        {
            < 100 => (0f, 45f, 0.10f),
            < 113 => (6f, 100f, 0.20f),
            _ => (8f, 113f, 0.50f)
        };

        return (int)(14.34f + (baseValue + (level - pivot) * slope) * 0.72f);
    }

    private static int ComputeDeco2AttackPowerBonus(EquippedItemSlot deco2)
    {
        if (deco2.Item.Sort == 2) return 0;
        var isWing =
            deco2.Item.ItemId is 213 or 214 or 215 or 217 or 218 or 2303 or 2304 or 2305;
        return (int)(deco2.Enchant * (isWing ? 23.4f : 11.7f));
    }

    private static int ApplyAttackBoostMultiplier(int atk, ConsumableContext consumable, ZoneContext zone)
    {
        return zone.ZoneNumber != BoostExcludedZoneNumber &&
               (consumable.DmgBoostActive || consumable.WarriorPillActive)
            ? (int)(atk * AttackBoostMultiplier)
            : atk;
    }
}
