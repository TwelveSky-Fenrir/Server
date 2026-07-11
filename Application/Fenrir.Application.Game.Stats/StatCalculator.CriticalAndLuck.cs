using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseCritical ----

    // WORKSTREAM B6 (Wave-6): cosmetic (costume-enchant cs -- CostumeCriticalContribution) is now live. mount
    // (grade whole-value multiplier on crit) context remains a B1 plumbing seam -- not read yet.
    private static int ComputeCritical(int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, MountContext mount = default)
    {
        var crit = 2; // base
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            crit += slot.Item.Critical;
            if (i != 8) // EPET
                crit += (int)(slot.Item.Critical *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).Critical);
        }

        if (bySlot[4] is { } ring && !IsLegendary(ring.Item)) // ring, slot 4 -- literal index
            crit += ring.Enchant / 4;

        crit += SetBonusTables.GetBaseCriticalFlatBonus(setNumber); // set 103 -> +1

        if (bySlot[10] is { } deco2)
            crit += deco2.Item.ItemId switch { 213 or 214 or 215 => 1, 216 or 217 or 218 => 3, _ => 0 };

        if (bySlot[8] is { } petAmulet)
            crit += PhoenixFlatBonus(petAmulet.Item.ItemId, 1, 2, 3);

        crit += CostumeCriticalContribution(cosmetic.CostumeEnchantCs); // B6 costume-enchant cs

        return crit;
    }

    // ---- GetBaseCriticalDefence ----

    // WORKSTREAM B6/B7 (Wave-6): cosmetic (stellar-core crit-defense) and zone (878 drunk -10%, CACHED leg)
    // are now live -- see the two terms below. mount (grade whole-value multiplier) remains a B1 seam.
    private static int ComputeCriticalDefence(int setNumber, int rebirthCount, int halo, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, MountContext mount = default, ZoneContext zone = default)
    {
        var critDef = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            critDef += slot.Item.CapeInfo2;
            if (i != 8) // EPET
                critDef += (int)(slot.Item.CapeInfo2 *
                                 SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).CriticalDefence);
        }

        critDef += SetBonusTables.GetBaseCriticalDefenceFlatBonus(setNumber);
        critDef += rebirthCount switch { <= 0 => 0, <= 6 => rebirthCount, _ => 6 }; // 7-12 -> +6

        critDef += halo == 96 ? 10 : halo / 10;

        critDef += SetBonusTables.CapeIuBonus(bySlot[1], 8, 0.5f);
        if (bySlot[1] is { } cape && cape.Item.ItemId == 1404)
            critDef += 30;

        if (bySlot[8] is { } petAmulet)
            critDef += PhoenixFlatBonus(petAmulet.Item.ItemId, 7, 9, 12);

        critDef += StellarCoreCriticalDefenceContribution(cosmetic); // B6 stellar core (narrower crit-def table)

        // B7 drunk-rage: 880 critical-defence -10%, CACHED leg applied to the fully summed base critical-defence
        // (after every additive contribution above) -- GetBaseCriticalDefence, MyFactor.cpp:3588-3589.
        critDef = ApplyDrunkCriticalDefence(critDef, zone);

        return critDef;
    }

    // ---- GetBaseLuck ----

    // WORKSTREAM B6 (Wave-6): cosmetic (costume valid-id +100 / costume-enchant cs*2) is now live.
    private static int ComputeLuck(int setNumber, EquippedItemSlot?[] bySlot, CosmeticContext cosmetic = default)
    {
        var luck = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            luck += slot.Item.Luck;
            if (i != 8) // EPET
                luck += (int)(slot.Item.Luck *
                              SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).Luck);
        }

        if (bySlot[0] is { } ring0 && !IsLegendary(ring0.Item))
            luck += 12 * ring0.Enchant;

        if (bySlot[1] is { } cape && cape.Item.ItemId == 1404)
            luck += 200;

        luck += CostumeLuckContribution(cosmetic.CostumeNumber, cosmetic.CostumeEnchantCs); // B6 costume

        return luck;
    }

    // ---- shared helpers ----

    private static int RebirthCriticalWrapperBonus(int rebirthCount)
    {
        // 1-6 -> +n; 7-11 -> +(n-6); 12 -> +8 (a jump, not a continuation of the pattern).
        return rebirthCount switch
        {
            <= 0 => 0,
            <= 6 => rebirthCount,
            <= 11 => rebirthCount - 6,
            _ => 8
        };
    }
}
