using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static int ComputeElementAttackPower(LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default,
        ZoneContext zone = default)
    {
        var eatk = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            eatk += slot.Item.ElementAttackPower;
            if (i != 8)
                eatk += (int)(slot.Item.ElementAttackPower *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).ElementAttackPower);
        }

        if (bySlot[4] is { } ring4)
        {
            var item = ring4.Item;
            if (IsLegendary(item))
            {
                var enchant = (int)ring4.Enchant;
                if (enchant > 100)
                    enchant -= 100;
                eatk += enchant * 200;
            }
            else if (item.CheckSetItem == 2)
            {
                eatk += SetBonusTables.LinearByCombine(ring4.Combine, 40);
            }
            else
            {
                eatk += IUEffectSlotContribution(5, item.Sort, item.Level, ring4.Combine);
            }
        }

        if (bySlot[10] is { } deco2 && ItemSortClass(deco2.Item) != 2)
        {
            var isWing = deco2.Item.ItemId is 210 or 211 or 212 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            eatk += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        eatk += levelRow.ElementAttack;

        eatk += ElementAttackElixirContribution(consumable, zone);
        eatk += StellarCoreElementAttackContribution(cosmetic);

        eatk = MountGradeElementAttack(eatk, mount);

        eatk += MountFlatElementAttack(mount);

        return eatk;
    }


    private static int ComputeElementDefensePower(int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default,
        ZoneContext zone = default)
    {
        var edef = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            edef += slot.Item.ElementDefensePower;
            if (i != 8)
                edef += (int)(slot.Item.ElementDefensePower *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).ElementDefensePower);
        }

        if (bySlot[0] is { } ring0 && !IsLegendary(ring0.Item) && ring0.Item.CheckSetItem == 2)
            edef += SetBonusTables.LinearByCombine(ring0.Combine, 40);

        if (bySlot[4] is { } ring4 && IsLegendary(ring4.Item))
        {
            var enchant = (int)ring4.Enchant;
            if (enchant > 100) enchant -= 100;
            edef += enchant * 200;
        }

        if (bySlot[10] is { } deco2 && ItemSortClass(deco2.Item) != 2)
        {
            var isWing = deco2.Item.ItemId is 207 or 208 or 209 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            edef += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        if (bySlot[0] is { } amuletIu6 && !IsLegendary(amuletIu6.Item) && amuletIu6.Item.CheckSetItem != 2)
            edef += IUEffectSlotContribution(6, amuletIu6.Item.Sort, amuletIu6.Item.Level, amuletIu6.Combine);

        edef += DecorationStatContribution(DecorationStatKind.ElementDefensePower, bySlot);

        edef += ElementDefenseElixirContribution(consumable, zone);
        edef += StellarCoreElementDefenseContribution(cosmetic);

        edef = MountGradeElementDefense(edef, mount);

        edef += MountFlatElementDefense(mount);

        return edef;
    }
}
