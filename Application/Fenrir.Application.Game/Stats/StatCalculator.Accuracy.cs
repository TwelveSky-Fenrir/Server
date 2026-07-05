using Fenrir.Data.World;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseAttackSuccess (HIT) ----

    private static int ComputeAttackSuccess(int strength, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
    {
        var hit = (int)(strength * 1.71f);
        hit += levelRow.AttackSuccess;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            hit += slot.Item.AttackSuccess;
            if (i != 8) // EPET: flat contribution above always counts, coefSet term skips slot 8
                hit += (int)(slot.Item.AttackSuccess *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackSuccess);
        }

        hit += ComputeGlovesAttackSuccessBonus(bySlot[3]);
        hit += ComputeWeaponAttackSuccessBonus(bySlot[7]);

        return hit;
    }

    private static int ComputeGlovesAttackSuccessBonus(EquippedItemSlot? glovesSlot)
    {
        if (glovesSlot is not { } gloves) return 0;
        var item = gloves.Item;
        var total = 0;

        if (IsLegendary(item))
        {
            var enchant = (int)gloves.Enchant;
            if (enchant >= 100) enchant -= 100;
            total += enchant * 1500;
        }

        if (item.CheckSetItem == 2)
        {
            total += SetBonusTables.LinearByCombine(gloves.Combine, 200);
            var enchant = gloves.Enchant;
            // 0<IS<=50 clamp assumed shared with every other set2 IS-combo term in this file.
            if (enchant is > 0 and <= 50)
                total += (int)(item.AttackSuccess * enchant * 0.03f);
        }

        return total;
    }

    private static int ComputeWeaponAttackSuccessBonus(EquippedItemSlot? weaponSlot)
    {
        return weaponSlot is { } weapon && weapon.Item.CheckSetItem == 2
            ? SetBonusTables.LinearByCombine(weapon.Combine, 60)
            : 0;
    }

    // ---- GetBaseAttackBlock (DODGE) ----

    private static int ComputeAttackBlock(int wisdom, int vitality, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
    {
        var dodge = (int)(wisdom * 1.67f) + (int)(vitality * 0.90f); // two separate truncations
        dodge += levelRow.AttackBlock;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            dodge += slot.Item.AttackBlock;
            if (i != 8) // EPET
                dodge += (int)(slot.Item.AttackBlock *
                               SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackBlock);
        }

        dodge += ComputeArmorAttackBlockBonus(bySlot[2]);
        dodge += ComputeBootsAttackBlockBonus(bySlot[5]);
        // Deco 9-12 sort2: ReturnNewStat(6) unread, contributes 0 beyond the generic loop above.

        return dodge;
    }

    private static int ComputeArmorAttackBlockBonus(EquippedItemSlot? armorSlot)
    {
        return armorSlot is { } armor && armor.Item.CheckSetItem == 2
            ? SetBonusTables.LinearByCombine(armor.Combine, 20)
            : 0;
    }

    private static int ComputeBootsAttackBlockBonus(EquippedItemSlot? bootsSlot)
    {
        if (bootsSlot is not { } boots) return 0;
        var item = boots.Item;
        var total = 0;

        if (IsLegendary(item))
        {
            var enchant = (int)boots.Enchant;
            if (enchant >= 100) enchant -= 100;
            total += enchant * 300;
        }

        if (item.CheckSetItem == 2)
        {
            total += SetBonusTables.LinearByCombine(boots.Combine, 80);
            var enchant = boots.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)(item.AttackBlock * enchant * 0.03f);
        }

        return total;
    }
}
