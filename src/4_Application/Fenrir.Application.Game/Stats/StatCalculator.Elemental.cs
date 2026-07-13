using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

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

            if (item.CheckSetItem == 2)
                eatk += SetBonusTables.LinearByCombine(ring4.Combine, 40);
        }

        if (bySlot[10] is { } deco2 && deco2.Item.Sort != 2)
        {
            var isWing = deco2.Item.ItemId is 210 or 211 or 212 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            eatk += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        eatk += levelRow.ElementAttack;

        // Ridden-mount grade multiplier (three-tier, element-damage marker); flat rolled bonus added at the end.
        eatk = MountGradeElementAttack(eatk, mount);

        eatk += ElementAttackElixirContribution(consumable, zone);
        eatk += StellarCoreElementAttackContribution(cosmetic);

        if (bySlot[4] is { } ringIu5)
            eatk += IUEffectSlotContribution(5, ringIu5.Item.Sort, ringIu5.Item.Level, ringIu5.Combine);

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

        if (bySlot[10] is { } deco2 && deco2.Item.Sort != 2)
        {
            var isWing = deco2.Item.ItemId is 207 or 208 or 209 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            edef += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        // Ridden-mount grade multiplier (three-tier, element-defense marker); flat rolled bonus added at the end.
        edef = MountGradeElementDefense(edef, mount);

        edef += ElementDefenseElixirContribution(consumable, zone);
        edef += StellarCoreElementDefenseContribution(cosmetic);

        if (bySlot[0] is { } amuletIu6)
            edef += IUEffectSlotContribution(6, amuletIu6.Item.Sort, amuletIu6.Item.Level, amuletIu6.Combine);

        edef += DecorationStatContribution(DecorationStatKind.ElementDefensePower, bySlot);

        edef += MountFlatElementDefense(mount);

        return edef;
    }
}
