using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- GetBaseElementAttackPower ----

    // WORKSTREAM B2 (consumable stat feed): the packed elemental-elixir counter's ATTACK sub-count
    // (consumable.EatElePotion / 1000) feeds element attack power here (+10/count, MyFactor.cpp:560), gated by
    // the elemental zone allowance on zone.ZoneNumber. The zone parameter is a trailing optional add: consumable
    // already flows from the call site, so the +10/count contribution is LIVE today, but with a default-eligible
    // zone -- the zone-84/eligibility gate only becomes precise once the ComputeElementAttackPower call site in
    // StatCalculator.ComputeBaseStats threads zone through (out of this workstream's scope, see wiringManifest).
    // WORKSTREAM B6 (Wave-6): cosmetic (stellar-core elemental attack) is now live. The mount grade multiplier
    // remains a B1 seam / not read here.
    private static int ComputeElementAttackPower(LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default,
        ZoneContext zone = default)
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

        eatk += ElementAttackElixirContribution(consumable, zone);
        eatk += StellarCoreElementAttackContribution(cosmetic); // B6 stellar core (shared EDMG/EDEF table)

        // B3-deco effect-sort 5 (element attack ramp): ring slot 4 only, IU count = Combine (MyFactor.cpp:3775).
        if (bySlot[4] is { } ringIu5)
            eatk += IUEffectSlotContribution(5, ringIu5.Item.Sort, ringIu5.Item.Level, ringIu5.Combine);

        return eatk;
    }

    // ---- GetBaseElementDefensePower ----

    // WORKSTREAM B2 (consumable stat feed): the packed elemental-elixir counter's DEFENSE sub-count
    // (consumable.EatElePotion % 1000) feeds element defense power here (+10/count, MyFactor.cpp:562), gated by
    // the elemental zone allowance on zone.ZoneNumber. Same trailing-optional zone note as
    // ComputeElementAttackPower above: the +10/count contribution is LIVE today with a default-eligible zone;
    // the zone-84/eligibility gate becomes precise once the call site threads zone (see wiringManifest).
    // WORKSTREAM B6 (Wave-6): cosmetic (stellar-core elemental defense) is now live. The mount grade multiplier
    // remains a B1 seam / not read here.
    private static int ComputeElementDefensePower(int setNumber, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default,
        ZoneContext zone = default)
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
            // EDEF's own wing-ID list (207/208/209), distinct from EATK's (210/211/212) -- confirmed
            // against Server/Header/Protocol/MyFactor.cpp:3941, contrasted with :3779 for EATK.
            var isWing = deco2.Item.ItemId is 207 or 208 or 209 or 216 or 217 or 218 or 2303 or 2304 or 2305;
            edef += (int)(deco2.Enchant * (isWing ? 7.8f : 3.9f));
        }

        edef += ElementDefenseElixirContribution(consumable, zone);
        edef += StellarCoreElementDefenseContribution(cosmetic); // B6 stellar core (shared EDMG/EDEF table)

        // B3-deco effect-sort 6 (element defense ramp): amulet slot 0 only, IU count = Combine (MyFactor.cpp:3918).
        if (bySlot[0] is { } amuletIu6)
            edef += IUEffectSlotContribution(6, amuletIu6.Item.Sort, amuletIu6.Item.Level, amuletIu6.Combine);

        // B3-deco decoration ReturnNewStat (slots 9-12, IS octet only -- see DecorationStatContribution remarks).
        edef += DecorationStatContribution(DecorationStatKind.ElementDefensePower, bySlot);

        // No LevelFactor for EDEF, unlike EATK.
        return edef;
    }
}
