using Fenrir.Data.World;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseElementAttackPower ----

    private static int ComputeElementAttackPower(LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot)
    {
        var eatk = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            eatk += slot.Item.ElementAttackPower;
            if (i != 8) // EPET
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
                    enchant -= 100; // >100 here, unlike weapon/armor's >=100 -- preserved verbatim
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

        return eatk;
    }

    // ---- GetBaseElementDefensePower ----

    private static int ComputeElementDefensePower(int setNumber, EquippedItemSlot?[] bySlot)
    {
        var edef = 0;
        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            edef += slot.Item.ElementDefensePower;
            if (i != 8) // EPET
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
            // Wing-ID list assumed shared with EATK's (not independently confirmed for EDEF).
            var isWing = deco2.Item.ItemId is 210 or 211 or 212 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            edef += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        // No LevelFactor for EDEF, unlike EATK.
        return edef;
    }
}
