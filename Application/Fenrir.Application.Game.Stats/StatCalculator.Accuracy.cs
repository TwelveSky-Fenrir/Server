using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseAttackSuccess (HIT) ----

    // mount (grade whole-value multiplier on hit) context is a B1 plumbing seam -- not read yet. WORKSTREAM B2:
    // zone/consumable are trailing optional adds -- the dexterity-elixir counter (consumable.EatDexPotion) feeds
    // hit at +2/elixir when the zone is elixir-eligible (MyFactor.cpp:694,699).
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
            if (i != 8) // EPET: flat contribution above always counts, coefSet term skips slot 8
                hit += (int)(slot.Item.AttackSuccess *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackSuccess);
        }

        hit += ComputeGlovesAttackSuccessBonus(bySlot[3]);
        hit += ComputeWeaponAttackSuccessBonus(bySlot[7]);

        // B7 layered the four-guild/event override on top of B2's raw +2/elixir floor (MyFactor.cpp:694,699).
        hit += AccuracyElixirContributionWithOverride(consumable, zone);

        // B3-deco effect-sort 3 (hit ramp): weapon slot only, IU count = Combine (MyFactor.cpp:3144).
        if (bySlot[7] is { } weaponIu3)
            hit += IUEffectSlotContribution(3, weaponIu3.Item.Sort, weaponIu3.Item.Level, weaponIu3.Combine);

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

    // mount (grade whole-value multiplier on block) context is a B1 plumbing seam -- not read yet. WORKSTREAM B2:
    // zone/consumable are trailing optional adds -- the dexterity-elixir counter (consumable.EatDexPotion) feeds
    // dodge at +2/elixir when the zone is elixir-eligible (MyFactor.cpp:726,731).
    private static int ComputeAttackBlock(int wisdom, int vitality, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot, MountContext mount = default, ZoneContext zone = default,
        ConsumableContext consumable = default)
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

        // B3-deco effect-sort 4 (dodge ramp): armor slot 2 AND boots slot 5, IU count = Combine
        // (MyFactor.cpp:3280, :3303-3304).
        if (bySlot[2] is { } armorIu4)
            dodge += IUEffectSlotContribution(4, armorIu4.Item.Sort, armorIu4.Item.Level, armorIu4.Combine);
        if (bySlot[5] is { } bootsIu4)
            dodge += IUEffectSlotContribution(4, bootsIu4.Item.Sort, bootsIu4.Item.Level, bootsIu4.Combine);

        // B3-deco decoration ReturnNewStat (slots 9-12, IS octet only -- see DecorationStatContribution remarks).
        dodge += DecorationStatContribution(DecorationStatKind.AttackBlock, bySlot);

        // B7 layered the four-guild/event override on top of B2's raw +2/elixir floor (MyFactor.cpp:726,731).
        dodge += BlockElixirContributionWithOverride(consumable, zone);

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
