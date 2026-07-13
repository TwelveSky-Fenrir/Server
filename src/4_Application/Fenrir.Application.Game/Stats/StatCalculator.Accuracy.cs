using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static int ComputeAttackSuccess(int strength, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot, MountContext mount = default, ZoneContext zone = default,
        ConsumableContext consumable = default)
    {
        var hit = (int)(strength * 1.71f);
        hit += levelRow.AttackSuccess;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            hit += slot.Item.AttackSuccess;
            if (i != 8)
                hit += (int)(slot.Item.AttackSuccess *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackSuccess);
        }

        // Ridden-mount grade multiplier (three-tier, hit marker); flat rolled bonus added at the end.
        hit = MountGradeHit(hit, mount);

        hit += ComputeGlovesAttackSuccessBonus(bySlot[3]);
        hit += ComputeWeaponAttackSuccessBonus(bySlot[7]);

        hit += AccuracyElixirContributionWithOverride(consumable, zone);

        if (bySlot[7] is { } weaponIu3)
            hit += IUEffectSlotContribution(3, weaponIu3.Item.Sort, weaponIu3.Item.Level, weaponIu3.Combine);

        hit += MountFlatHit(mount);

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


    private static int ComputeAttackBlock(int wisdom, int vitality, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot, MountContext mount = default, ZoneContext zone = default,
        ConsumableContext consumable = default)
    {
        var dodge = (int)(wisdom * 1.67f) + (int)(vitality * 0.90f);
        dodge += levelRow.AttackBlock;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            dodge += slot.Item.AttackBlock;
            if (i != 8)
                dodge += (int)(slot.Item.AttackBlock *
                               SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackBlock);
        }

        // Ridden-mount grade multiplier (three-tier, dodge marker); flat rolled bonus added at the end.
        dodge = MountGradeDodge(dodge, mount);

        dodge += ComputeArmorAttackBlockBonus(bySlot[2]);
        dodge += ComputeBootsAttackBlockBonus(bySlot[5]);

        if (bySlot[2] is { } armorIu4)
            dodge += IUEffectSlotContribution(4, armorIu4.Item.Sort, armorIu4.Item.Level, armorIu4.Combine);
        if (bySlot[5] is { } bootsIu4)
            dodge += IUEffectSlotContribution(4, bootsIu4.Item.Sort, bootsIu4.Item.Level, bootsIu4.Combine);

        dodge += DecorationStatContribution(DecorationStatKind.AttackBlock, bySlot);

        dodge += BlockElixirContributionWithOverride(consumable, zone);

        dodge += MountFlatDodge(mount);

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
