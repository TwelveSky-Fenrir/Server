using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static int ComputeCritical(int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, MountContext mount = default)
    {
        var crit = 2;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            crit += slot.Item.Critical;
            if (i != 8)
                crit += (int)(slot.Item.Critical *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).Critical);
        }

        // Ridden-mount grade multiplier (three-tier, critical marker). Critical is the one stat with a grade
        // multiplier but no flat rolled bonus (the rolled system has eight attributes and Critical is not one).
        crit = MountGradeCritical(crit, mount);

        if (bySlot[4] is { } ring && !IsLegendary(ring.Item))
            crit += ring.Enchant / 4;

        crit += SetBonusTables.GetBaseCriticalFlatBonus(setNumber);

        if (bySlot[10] is { } deco2)
            crit += deco2.Item.ItemId switch { 213 or 214 or 215 => 1, 216 or 217 or 218 => 3, _ => 0 };

        if (bySlot[8] is { } petAmulet)
            crit += PhoenixFlatBonus(petAmulet.Item.ItemId, 1, 2, 3);

        crit += CostumeCriticalContribution(cosmetic.CostumeEnchantCs);

        return crit;
    }


    private static int ComputeCriticalDefence(int setNumber, int rebirthCount, int halo, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, MountContext mount = default, ZoneContext zone = default)
    {
        var critDef = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            critDef += slot.Item.CapeInfo2;
            if (i != 8)
                critDef += (int)(slot.Item.CapeInfo2 *
                                 SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).CriticalDefence);
        }

        critDef += SetBonusTables.GetBaseCriticalDefenceFlatBonus(setNumber);
        critDef += rebirthCount switch { <= 0 => 0, <= 6 => rebirthCount, _ => 6 };

        critDef += halo == 96 ? 10 : halo / 10;

        critDef += SetBonusTables.CapeIuBonus(bySlot[1], 8, 0.5f);
        if (bySlot[1] is { } cape && cape.Item.ItemId == 1404)
            critDef += 30;

        if (bySlot[8] is { } petAmulet)
            critDef += PhoenixFlatBonus(petAmulet.Item.ItemId, 7, 9, 12);

        critDef += StellarCoreCriticalDefenceContribution(cosmetic);

        critDef = ApplyDrunkCriticalDefence(critDef, zone);

        return critDef;
    }


    private static int ComputeLuck(int setNumber, EquippedItemSlot?[] bySlot, CosmeticContext cosmetic = default)
    {
        var luck = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            luck += slot.Item.Luck;
            if (i != 8)
                luck += (int)(slot.Item.Luck *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).Luck);
        }

        if (bySlot[0] is { } ring0 && !IsLegendary(ring0.Item))
            luck += 12 * ring0.Enchant;

        if (bySlot[1] is { } cape && cape.Item.ItemId == 1404)
            luck += 200;

        luck += CostumeLuckContribution(cosmetic.CostumeNumber, cosmetic.CostumeEnchantCs);

        return luck;
    }


    private static int RebirthCriticalWrapperBonus(int rebirthCount)
    {
        return rebirthCount switch
        {
            <= 0 => 0,
            <= 6 => rebirthCount,
            <= 11 => rebirthCount - 6,
            _ => 8
        };
    }
}
