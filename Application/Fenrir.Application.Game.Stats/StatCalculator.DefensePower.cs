using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseDefensePower ----

    // WORKSTREAM B6/B7 (Wave-6): cosmetic (stellar-core defense) and zone (ornament DEF) are now live -- see
    // the two terms below. mount (grade whole-value multiplier) remains a B1 seam / not read here.
    private static int ComputeDefensePower(int wisdom, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ZoneContext zone = default, MountContext mount = default)
    {
        var def = (int)(wisdom * 9.63f); // fDef = 1.63 + 8.0 (MY_DEF active) = 9.63
        def += levelRow.DefensePower;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            def += slot.Item.DefensePower;
            if (i != 8) // EPET: coefSet multiplier skipped, flat += above still applies
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
            def -= petAmulet.Item.DefensePower; // Phoenix replaces the item's own DefensePower stat
            def += PhoenixFlatBonus(petAmulet.Item.ItemId, 5000, 7500, 12500);
        }

        def += SetBonusTables.GetBaseFlatDefensePowerBonus(setNumber); // NXT +1000/2000/3000

        if (bySlot[1] is { } capeSlot)
            def += capeSlot.Item.ItemId switch { 1404 => 2200, 1401 => 650, _ => 0 };

        // The legacy has a second, separate Phoenix DEF add on top of the replace above -- preserved verbatim.
        if (bySlot[8] is { } petAmuletFinal)
            def += PhoenixFlatBonus(petAmuletFinal.Item.ItemId, 2000, 4500, 9500);

        def += StellarCoreDefensePowerContribution(cosmetic); // B6 stellar core (shared DMG/DEF table)
        def += OrnamentDefenseContribution(zone, bySlot); // B7 ornament ORN_DEF

        // B3-deco effect-sort 2 (defense power ramp): cape slot only, IU count = Combine (MyFactor.cpp:2846).
        if (bySlot[1] is { } capeIu2)
            def += IUEffectSlotContribution(2, capeIu2.Item.Sort, capeIu2.Item.Level, capeIu2.Combine);

        // B3-deco decoration ReturnNewStat (slots 9-12, IS octet only -- see DecorationStatContribution remarks).
        def += DecorationStatContribution(DecorationStatKind.DefensePower, bySlot);

        return def;
    }

    private static int ComputeCapeDefensePowerBonus(EquippedItemSlot? capeSlot)
    {
        if (capeSlot is not { } cape) return 0;
        var total = 0;
        if (cape.Item.Sort == 29) total += 6 * cape.Enchant;
        if (cape.Item.CheckSetItem == 2) total += SetBonusTables.CapeDefenseByCombine(cape.Combine);
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
            total += enchant * 1000;
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
        if (deco2.Item.Sort == 2)
            return 0; // deco (sort==2) items route through ReturnNewStat instead -- see DecorationStatContribution
        var isSpecial = deco2.Item.ItemId is 204 or 205 or 206 or 216 or 217 or 218 or 2303 or 2304 or 2305;
        return (int)(deco2.Enchant * (isSpecial ? 48.75f : 24.35f));
    }
}
