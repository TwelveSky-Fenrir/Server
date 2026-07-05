namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseAttackPower ----

    private static int ComputeAttackPower(int strength, int ki, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
    {
        var weaponSlot = bySlot[7];
        var coefficients = ResolveWeaponAttackCoefficients(weaponSlot);
        // Two separate truncations for Str and Ki -- NOT (int)(str*fStr + ki*fKi).
        var atk = (int)(strength * coefficients.Str) + (int)(ki * coefficients.Ki);
        atk += levelRow.AttackPower;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            atk += slot.Item.AttackPower;
            if (i != 8) // EPET: coefSet multiplier skipped, flat += above still applies
                atk += (int)(slot.Item.AttackPower *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackPower);
        }

        if (bySlot[1] is { } capeSlot)
        {
            if (capeSlot.Item.Sort == 29) atk += 6 * capeSlot.Enchant;
            atk += capeSlot.Item.ItemId switch { 1404 => 1100, 1401 => 100, _ => 0 };
        }

        atk += ComputeWeaponAttackPowerBonus(weaponSlot);

        if (bySlot[10] is { } deco2)
            atk += ComputeDeco2AttackPowerBonus(deco2);
        // Deco 9/11/12 (and deco2's own sort==2 branch): ReturnNewStat unread, contributes 0 beyond the generic loop above.

        if (bySlot[8] is { } petAmulet)
        {
            atk -= petAmulet.Item
                .AttackPower; // Phoenix removes the item's own AttackPower stat, undoing the flat += above
            atk += PhoenixFlatBonus(petAmulet.Item.ItemId, 3000, 4000, 5000);
        }

        atk += SetBonusTables.GetBaseFlatAttackPowerBonus(setNumber); // NXT +500/1000/1500

        return atk;
    }

    private static (float Str, float Ki) ResolveWeaponAttackCoefficients(EquippedItemSlot? weapon)
    {
        // Values already include the MY_DMG_CAL +1.0f active in prod.
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
                enchant -= 100; // weapon uses >=100 (differs from the amulet's >100)
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
            // Only ReturnIUEffectValue effect-sort 1 is faithfully reproducible -- see class remarks.
            var effect = WeaponAttackEffectValue(item);
            var e = effect * weapon.Combine;
            total += e;
            var enchant = weapon.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)((e + item.AttackPower) * enchant * 0.03f);
        }

        return total;
    }

    /// <summary>
    ///     ReturnIUEffectValue, effect-sort 1 (weapon attack): uses the weapon item's own Level column, not the
    ///     character's level.
    /// </summary>
    private static int WeaponAttackEffectValue(ItemRowDto weapon)
    {
        if (weapon.Sort != 4 && weapon.Sort is < 13 or > 21) return 0;

        var level = (float)weapon.Level;
        var (baseValue, pivot, slope) = level switch
        {
            < 100 => (0f, 45f, 0.10f),
            < 113 => (6f, 100f, 0.20f),
            _ => (8f, 113f, 0.50f) // < 146 always holds for real data (world.Levels is gapless 1..145)
        };

        return (int)(14.34f + (baseValue + (level - pivot) * slope) * 0.72f);
    }

    private static int ComputeDeco2AttackPowerBonus(EquippedItemSlot deco2)
    {
        if (deco2.Item.Sort == 2) return 0; // ReturnNewStat unread
        var isWing =
            deco2.Item.ItemId is 213 or 214 or 215 or 217 or 218 or 2303 or 2304 or 2305; // NOT 216, unlike DEF/EATK
        return (int)(deco2.Enchant * (isWing ? 23.4f : 11.7f));
    }
}
