using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static int ComputeDefensePower(int wisdom, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ZoneContext zone = default, MountContext mount = default)
    {
        var def = (int)(wisdom * 9.63f);
        def += OrnamentDefenseContribution(zone, bySlot);
        def += levelRow.DefensePower;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            def += slot.Item.DefensePower;
            if (i != 8)
                def += (int)(slot.Item.DefensePower *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).DefensePower);
        }

        def += ComputeCapeDefensePowerBonus(bySlot[1]);
        def += ComputeArmorDefensePowerBonus(bySlot[2]);
        def += ComputeGlovesDefensePowerBonus(bySlot[3]);
        def += ComputeBootsDefensePowerBonus(bySlot[5]);

        if (bySlot[10] is { } deco2)
            def += ComputeDeco2DefensePowerBonus(deco2);

        if (bySlot[8] is { } petAmulet)
        {
            def -= petAmulet.Item.DefensePower;
            def += PetAmuletDefenseBonus(petAmulet.Item.ItemId, petAmulet.Item.Sort);
        }

        def += DecorationStatContribution(DecorationStatKind.DefensePower, bySlot);

        def += StellarCoreDefensePowerContribution(cosmetic);

        def = MountGradeDefense(def, mount);

        def += SetBonusTables.GetBaseFlatDefensePowerBonus(setNumber);

        def += MountFlatDefense(mount);

        return def;
    }

    private static int ComputeCapeDefensePowerBonus(EquippedItemSlot? capeSlot)
    {
        if (capeSlot is not { } cape) return 0;

        var total = 6 * cape.Enchant;
        if (cape.Item.Sort == 29)
            return total;

        total += cape.Item.CheckSetItem == 2
            ? SetBonusTables.CapeDefenseByCombine(cape.Combine)
            : IUEffectSlotContribution(2, cape.Item.Sort, cape.Item.Level, cape.Combine);

        return total;
    }

    private static int ComputeArmorDefensePowerBonus(EquippedItemSlot? armorSlot)
    {
        if (armorSlot is not { } armor) return 0;
        var item = armor.Item;
        var total = 0;

        if (IsLegendary(item))
        {
            var enchant = (int)armor.Enchant;
            if (enchant >= 100) enchant -= 100;
            return enchant * 1000;
        }

        if (item.CheckSetItem == 2)
        {
            total += SetBonusTables.LinearByCombine(armor.Combine, 300);
            var enchant = armor.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)(item.DefensePower * enchant * 0.03f);
        }

        return total;
    }

    private static int ComputeGlovesDefensePowerBonus(EquippedItemSlot? glovesSlot)
    {
        return glovesSlot is { } gloves && gloves.Item.CheckSetItem == 2
            ? SetBonusTables.LinearByCombine(gloves.Combine, 50)
            : 0;
    }

    private static int ComputeBootsDefensePowerBonus(EquippedItemSlot? bootsSlot)
    {
        return bootsSlot is { } boots && boots.Item.CheckSetItem == 2
            ? SetBonusTables.LinearByCombine(boots.Combine, 30)
            : 0;
    }

    private static int ComputeDeco2DefensePowerBonus(EquippedItemSlot deco2)
    {
        if (ItemSortClass(deco2.Item) == 2)
            return 0;
        var isSpecial = deco2.Item.ItemId is 204 or 205 or 206 or 216 or 217 or 218 or 2303 or 2304 or 2305;
        return (int)(deco2.Enchant * (isSpecial ? 48.75f : 24.35f));
    }
}
