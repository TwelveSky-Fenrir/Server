using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // ---- Elixir / consumable stat contributions (WORKSTREAM B2) ----
    //
    // MyFactor::GetZoneForElixir (Server/Header/Protocol/MyFactor.cpp:549-739) folds the five persisted elixir
    // counters into recomputed DERIVED stats -- never into base attributes, and never as a multiplier. Each is a
    // flat, additive whole-number amount, computed independently per stat:
    //   life elixir -> +20 / elixir into max HP             (MyFactor.cpp:595,600)
    //   mana elixir -> +25 / elixir into max MP             (MyFactor.cpp:627,632)
    //   str  elixir -> +3  / elixir into attack power       (MyFactor.cpp:657,662)   (ComputeAttackPower)
    //   dex  elixir -> +2  / elixir into accuracy AND block (MyFactor.cpp:694,699 / :726,731) -- two stats
    //   ele  elixir -> +10 / count into element atk / def   (MyFactor.cpp:560,562)   -- packed counter, 2 counts
    // The counters are already capped on the consume side (200, or 400 for a grade-12/high-level character), so
    // these amounts are inherently bounded.
    //
    // WORKSTREAM B7 (Wave-6) layered the max-potion-event tier and the official-guild ("B4G") fixed override on
    // top of the raw per-elixir floor below -- see StatCalculator.FourGuildElixirContribution.cs's
    // *ElixirContributionWithOverride methods, which every non-elemental getter now calls instead of the plain
    // *ElixirContribution helpers below (confirmed still true as of B7-fourguild-eventtier-source, wave14: every
    // one of the 5 call sites below already reads the WithOverride variant). Both overrides still degrade to the
    // exact raw floor today, but NOT because their extra inputs are merely "not yet threaded" -- wave14 traced
    // every write site for the four B4G opt-in flags, the world four-guild-name table, and the event tribe/tier
    // settings across all 9 executables plus Header/Lib and confirmed each is provably always at its
    // constructor-default/reset value in the compiled legacy build (zero live write sites; no DB persistence
    // path for the B4G flags either). There is no PlayerRuntimeState field to add here that would ever hold a
    // real value -- closing this further is a product decision (inventing a new Fenrir-only activation
    // mechanism), not a citation gap. See ConsumableContext.MaxPotionEventNum/EventTribe's own remarks and
    // StatCalculator.FourGuildElixirContribution.cs's "MISSING INPUTS" note for the full citation trail. The
    // elemental family is NOT part of the override (its branch runs before the four-guild slot is ever
    // resolved), so ElementAttackElixirContribution/ElementDefenseElixirContribution below stay the live, final
    // call sites.

    private const int LifeElixirRate = 20; // MyFactor.cpp:595,600
    private const int ManaElixirRate = 25; // MyFactor.cpp:627,632
    private const int StrengthElixirAttackRate = 3; // MyFactor.cpp:657,662

    private const int ElementElixirRate = 10; // MyFactor.cpp:560,562
    // ---- GetBaseMaxLife ----

    // WORKSTREAM B2 (consumable stat feed): the life-elixir counter (consumable.EatLifePotion) is now read via
    // LifeElixirContributionWithOverride below (B7 layered the four-guild/event override on top of B2's raw
    // floor), gated by the elixir zone-eligibility test on zone.ZoneNumber. WORKSTREAM B6/B7 (Wave-6): the
    // ornament HP bonus, the HP-feeding rank-buff tier, the Stellar-Core HP table, and the 878 drunk -10% are
    // now live too (see the four terms below). WORKSTREAM B12: the FFA (zone 335) terminal override now
    // discards every term above and returns a fixed 100000 -- see ApplyFreeForAllMaxLife at the tail. Still
    // not read here: the HP-boost/warrior-pill 1.2x max-life multiplier (consumable.HpBoostActive/
    // WarriorPillActive, MyFactor.cpp:1941-1943 -- a separate contract) and the mount grade whole-value
    // multiplier (B8-mount Tier 1, blocked -- see MountGradeContribution.cs).
    private static int ComputeMaxLife(int vitality, LevelRowDto levelRow, int setNumber, bool isLegendarySet,
        byte previousTribe, EquippedItemSlot?[] bySlot, int petLife,
        ZoneContext zone = default, ConsumableContext consumable = default, MountContext mount = default,
        CosmeticContext cosmetic = default)
    {
        var hp = (int)(vitality * 20.0f);
        hp += levelRow.Life; // ornament/deco/elixir bonuses unmodeled, skipped
        hp = ApplyPetDoubleRule(hp, petLife); // HP-boost-pill/animal-grade/mount bonuses unmodeled

        hp += SetBonusTables.GetFlatLifeBonus(setNumber);
        hp += ComputeG12CustomSetBonus(previousTribe, bySlot);
        if (isLegendarySet) hp += 30000;

        if (bySlot[0] is { } amulet) // slot EAMULET(0) -- literal index, see naming-inversion note on EquippedItemSlot
        {
            var enchant = (int)amulet.Enchant;
            if (enchant > 0)
            {
                if (enchant > 100) enchant -= 100;
                hp += 500 * enchant;
            }
        }

        hp += ComputeIsIuForLifeBonus(bySlot);
        hp += ComputeG12LifeUpBonus(bySlot);
        hp += SetBonusTables.CapeIuBonus(bySlot[1], 3, 200f);

        if (bySlot[8] is { } petAmulet)
        {
            // Two separate, unconditional additions -- confirmed to STACK, not alternate (workstream
            // pet-amulet-bonus-table-mapping, 2026-07-11): the base amulet Life table (ReturnAmuletLifeValue,
            // GameSystem_07_Pet.cpp:768-770) plus the wholly independent "Custom Phoenix Amulet final HP
            // correction" (MyFactor.cpp:2202-2214). Net for 76005/76006/76007: 7000/12000/22000 -- mirrors the
            // already-correct two-pass shape in ComputeDefensePower (same 5000/7500/12500 then 2000/4500/9500).
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 5000, 7500, 12500);
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);
        }

        // Flat, additive, applied after every multiplicative step (pet-double, coefficients) -- an elixir
        // contribution is never a multiplier and is folded on top of the stat from all other sources.
        hp += LifeElixirContributionWithOverride(consumable, zone);
        hp += StellarCoreMaxLifeContribution(cosmetic); // B6 stellar core (tier-2-only table)
        hp += OrnamentLifeContribution(zone, bySlot); // B7 ornament ORN_HP
        hp += RankBuffMaxLifeBonus(zone); // B7 rank-buff tier 6

        // B7 drunk-rage: 878 max-life -10%, CACHED leg applied to the fully summed base max life (after every
        // additive contribution above) -- GetBaseMaxLife, MyFactor.cpp:2184-2185.
        hp = ApplyDrunkMaxLife(hp, zone);

        // B3-deco decoration ReturnNewStat (slots 9-12, IS octet only -- see DecorationStatContribution remarks,
        // MyFactor.cpp:1928-1931).
        hp += DecorationStatContribution(DecorationStatKind.MaxLife, bySlot);

        // B12 fix: FFA (zone 335) is a TERMINAL hard override -- the computed max-life above (including the
        // drunk leg) is discarded and a fixed 100000 returned (MyFactor.cpp:2216-2221).
        return ApplyFreeForAllMaxLife(hp, zone.ZoneNumber);
    }

    /// <summary>
    ///     G12 custom-set bonus, gated on all 6 canonical slots being in-tribe IDs. Keyed on
    ///     <paramref name="previousTribe" /> (aPreviousTribe), not the character's current playable tribe --
    ///     Server/Header/Protocol/MyFactor.cpp:2032-2094.
    /// </summary>
    private static int ComputeG12CustomSetBonus(byte previousTribe, EquippedItemSlot?[] bySlot)
    {
        var range = previousTribe switch
        {
            0 => (Lo: 84500, Hi: 84699),
            1 => (Lo: 85500, Hi: 85699),
            2 => (Lo: 86500, Hi: 86699),
            _ => (Lo: 0, Hi: -1)
        };
        if (range.Hi < range.Lo) return 0;

        int[] relevantSlots = [0, 2, 3, 4, 5, 7];
        var minCombine = -1;
        foreach (var slotIndex in relevantSlots)
        {
            if (bySlot[slotIndex] is not { } slot) return 0;
            var id = slot.Item.ItemId;
            if (id < range.Lo || id > range.Hi) return 0;
            if (minCombine < 0 || slot.Combine < minCombine) minCombine = slot.Combine;
        }

        return minCombine switch { >= 12 => 15000, >= 6 => 5000, _ => 0 };
    }

    /// <summary>sort==1 items at slots 0-7 except cape(1)/null(6) -- specifically sort==1, not the usual {1,4} legendary set.</summary>
    private static int ComputeIsIuForLifeBonus(EquippedItemSlot?[] bySlot)
    {
        var total = 0;
        for (var i = 0; i <= 7; i++)
        {
            if (i is 1 or 6) continue;
            if (bySlot[i] is not { } slot) continue;
            if (slot.Item.Sort != 1)
                continue;

            var d = slot.Combine / 10;
            var u = slot.Combine % 10;
            // Confirmed range D in 1..5, U in 1..5 -- not extrapolated beyond that.
            if (d is >= 1 and <= 5 && u is >= 1 and <= 5) total += d * 1000;
        }

        return total;
    }

    /// <summary>ReturnSetItemIUValue_LifeUp: G12 (MartialLevelLimit==12) non-legendary pieces.</summary>
    private static int ComputeG12LifeUpBonus(EquippedItemSlot?[] bySlot)
    {
        var v5 = 0;
        var v7 = 0;
        for (var i = 0; i <= 7; i++)
        {
            if (i is 1 or 6) continue;
            if (bySlot[i] is not { } slot) continue;
            if (slot.Item.MartialLevelLimit != 12) continue;
            if (IsLegendary(slot.Item)) continue;
            if (slot.Combine >= 6) v5++;
            if (slot.Combine >= 12) v7++;
        }

        // v5>=6 -> +5000; v7>=6 -> +15000 more (6 CS12 pieces = +20000 total).
        return (v5 >= 6 ? 5000 : 0) + (v7 >= 6 ? 15000 : 0);
    }

    // ---- GetBaseMaxMana ----

    // WORKSTREAM B2 (consumable stat feed): the mana-elixir counter (consumable.EatManaPotion) is now read via
    // ManaElixirContributionWithOverride below (B7 layered the four-guild/event override on top of B2's raw
    // floor), gated by the elixir zone-eligibility test on zone.ZoneNumber. WORKSTREAM B7: the ornament MP
    // bonus (zone) is now live too. WORKSTREAM B12: the FFA (zone 335) terminal override now discards every
    // term above and returns a fixed 30000 -- see ApplyFreeForAllMaxMana at the tail. The mount grade
    // whole-value multiplier remains blocked -- not read here.
    private static int ComputeMaxMana(int ki, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        int petMana, ZoneContext zone = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var mp = (int)(ki * 15.3100004196167f); // exact source literal, not a rounded 15.31f
        mp += levelRow.Mana;
        mp = ApplyPetDoubleRule(mp, petMana);

        mp += SetBonusTables.GetFlatManaBonus(setNumber);
        mp += SetBonusTables.CapeIuBonus(bySlot[1], 4, 250f);

        if (bySlot[1] is { } cape)
            mp += cape.Item.ItemId switch { 1401 => 50, 1404 => 100, _ => 0 };

        if (bySlot[8] is { } petAmulet)
        {
            // Same two-pass stacking as ComputeMaxLife above (base amulet Mana table, GameSystem_07_Pet.cpp:
            // 848-850, plus the independent MyFactor.cpp:2335-2347 correction) -- net 7000/12000/22000.
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 5000, 7500, 12500);
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);
        }

        mp += ManaElixirContributionWithOverride(consumable, zone);
        mp += OrnamentManaContribution(zone, bySlot); // B7 ornament ORN_MP

        // B3-deco decoration ReturnNewStat (slots 9-12, IS octet only -- see DecorationStatContribution remarks,
        // MyFactor.cpp:2245-2248).
        mp += DecorationStatContribution(DecorationStatKind.MaxMana, bySlot);

        // B12 fix: FFA (zone 335) is a TERMINAL hard override -- the computed max-mana above is discarded and a
        // fixed 30000 returned (MyFactor.cpp:2349-2354).
        return ApplyFreeForAllMaxMana(mp, zone.ZoneNumber);
    }

    /// <summary>
    ///     Elixir zone-eligibility gate for the non-elemental families (MyFactor.cpp:554). Legacy: a zone is
    ///     eligible when it is neither a "level battle zone" nor a "267-type zone", OR when its number is in the
    ///     319..323 re-enable band. Those two suppressing classifications are an opaque zone-classification
    ///     surface <see cref="ZoneContext" /> does not carry, so their suppression is DEFERRED (defaults to
    ///     eligible, the common case) until a classifier is threaded in -- only the concrete 319..323 band is
    ///     modeled. See the workstream's openQuestions.
    /// </summary>
    private static bool IsElixirEligibleZone(short zoneNumber)
    {
        if (zoneNumber is >= 319 and <= 323)
            return true;
        return !IsElixirSuppressedZone(zoneNumber);
    }

    /// <summary>
    ///     "level battle zone" / "267-type zone" membership -- an opaque legacy zone-classification surface not
    ///     carried by <see cref="ZoneContext" />. Returns false (no suppression) until a real zone classifier is
    ///     threaded in; this is the single wiring point for that follow-up. TODO(B2), see openQuestions.
    /// </summary>
    private static bool IsElixirSuppressedZone(short zoneNumber)
    {
        return false;
    }

    /// <summary>
    ///     Additional elemental-only zone allowance (MyFactor.cpp:557): the elemental contribution requires base
    ///     elixir-eligibility AND that the zone is outside a further excluded set -- concretely not zone 84, plus
    ///     further bands the contract cites only as "roughly 235..249 / 292..294". Zone 84 is modeled; the
    ///     approximate bands are NOT hardcoded (exact bounds unrecoverable from the contract). TODO(B2), see
    ///     openQuestions.
    /// </summary>
    private static bool IsElementElixirZoneAllowed(short zoneNumber)
    {
        return IsElixirEligibleZone(zoneNumber) && zoneNumber != 84;
    }

    /// <summary>
    ///     Packed aEatElePotion decode (MyUtil::GetEatElementPotion, S07_MyGame03.cpp:9132-9139): the
    ///     elemental-attack count is the whole-thousands part of the packed value.
    /// </summary>
    private static int DecodeElementAttackCount(int packedElePotion)
    {
        return packedElePotion / 1000;
    }

    /// <summary>
    ///     Packed aEatElePotion decode: the elemental-defense count is the remainder after removing whole
    ///     thousands.
    /// </summary>
    private static int DecodeElementDefenseCount(int packedElePotion)
    {
        return packedElePotion % 1000;
    }

    // LifeElixirContribution/ManaElixirContribution/StrengthElixirAttackContribution (the plain B2 raw-floor
    // helpers) were retired here once B7's *ElixirContributionWithOverride methods replaced their only call
    // sites (ComputeMaxLife/ComputeMaxMana/ComputeAttackPower) -- the override methods reproduce the exact
    // same raw-floor arithmetic (LifeElixirRate/ManaElixirRate/StrengthElixirAttackRate * potionCount) as their
    // final fallback layer, so no behavior was lost. AccuracyElixirContributionWithOverride and
    // BlockElixirContributionWithOverride (StatCalculator.FourGuildElixirContribution.cs) are the equivalent
    // replacements for the dexterity-elixir inline expressions that used to live directly in
    // StatCalculator.Accuracy.cs's ComputeAttackSuccess/ComputeAttackBlock.

    private static int ElementAttackElixirContribution(ConsumableContext consumable, ZoneContext zone)
    {
        return IsElementElixirZoneAllowed(zone.ZoneNumber)
            ? ElementElixirRate * DecodeElementAttackCount(consumable.EatElePotion)
            : 0;
    }

    private static int ElementDefenseElixirContribution(ConsumableContext consumable, ZoneContext zone)
    {
        return IsElementElixirZoneAllowed(zone.ZoneNumber)
            ? ElementElixirRate * DecodeElementDefenseCount(consumable.EatElePotion)
            : 0;
    }
}
