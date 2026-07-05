using System.Collections.Frozen;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Pure C# port of the legacy MyFactor stat-calculation engine. No I/O: every input is a plain value the
///     caller already has in memory. Two layers, matching the legacy's own split:
///     <see cref="ComputeBaseStats" /> (the SetBasicAbilityFromEquip cache -- recompute only on
///     equipment/level/title/halo change) and <see cref="ComputeEffectiveStats" /> (buffs, pet-double, and
///     set/title/cape adjustments layered on top).
///     Every C++ <c>(int)</c> truncation in the source is preserved as its own explicit cast in the same
///     relative position: MyFactor's variables are real C++ int accumulators, so
///     <c>(int)(a*x) + (int)(b*y)</c> is not the same value as <c>(int)(a*x + b*y)</c> (see
///     <see cref="ComputeAttackPower" />'s two separate casts for Str and Ki).
/// </summary>
/// <remarks>
///     Not implemented (each contributes 0, like a compiled-out legacy feature): zone-context bonuses
///     (elixirs, ornaments, boost pills, rank buffs, zone038/FFA overrides); costumes and the rune system (no
///     PlayerRuntimeState fields yet); the animal/mount system and full PETSYSTEM beyond
///     <see cref="PetStatContribution" />; ReturnIUEffectValue effect-sorts 2-6 (only effect-sort 1,
///     <see cref="WeaponAttackEffectValue" />, is transcribed); ITEMSYSTEM::ReturnNewStat for deco sort==2;
///     GetSocketInfo (body never located); Stellar Core, Phoenix Growth/IM, GIFT_EVENT amulet values, drunk
///     potions, rage buff, tribe-role/mix-skill bonuses; legacy set-number detection for sets 1-22/30/50/51
///     (only NXT detection is implemented, see <see cref="SetBonusTables" />); speeds (no GetSpeed exists in
///     MyFactor at all).
/// </remarks>
public static class StatCalculator
{
    // The 5 title-rank vectors (rank 1..14), reused across the 4 base stats' own tranche tables.
    private static readonly int[] TitleTableA = [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 67, 82, 97, 112];
    private static readonly int[] TitleTableB = [0, 1, 3, 5, 8, 11, 14, 18, 23, 28, 34, 41, 48, 55];
    private static readonly int[] TitleTableC = [2, 6, 12, 20, 30, 42, 56, 72, 90, 110, 134, 164, 194, 224];
    private static readonly int[] TitleTableD = [1, 2, 3, 5, 7, 10, 14, 18, 22, 27, 33, 41, 49, 57];
    private static readonly int[] TitleTableE = [3, 8, 15, 25, 37, 52, 70, 90, 112, 137, 167, 205, 243, 281];

    private static readonly LevelRowDto ZeroLevelRow = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     The legacy "pet double" rule: if the running total already meets the pet's own contribution, add it; otherwise
    ///     double the whole running total instead.
    /// </summary>
    public static int ApplyPetDoubleRule(int statValue, int petStatValue)
    {
        return statValue >= petStatValue ? statValue + petStatValue : statValue * 2;
    }

    /// <summary>The cached "base" stat snapshot: recompute on equipment/level/title/halo change, never once per tick.</summary>
    /// <param name="legacySetNumber">
    ///     Pre-computed mSetNumber for sets 1-22/30/50/51 (0 = none) -- see <see cref="SetBonusTables" /> for
    ///     why this calculator does not detect those itself. NXT is detected internally and takes priority
    ///     when it matches.
    /// </param>
    public static EffectiveStats ComputeBaseStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        int legacySetNumber = 0,
        PetStatContribution pet = default)
    {
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.Tribe, equipment, legacySetNumber);
        var isLegendarySet = AnyLegendary(bySlot);
        var levelRow = GetLevelRow(levels, attributes.Level);

        var vitality = ComputeVitality(attributes, bySlot);
        var strength = ComputeStrength(attributes, bySlot);
        var ki = ComputeKi(attributes, bySlot);
        var wisdom = ComputeWisdom(attributes, bySlot);

        return new EffectiveStats(
            ComputeMaxLife(vitality, levelRow, setNumber, isLegendarySet, attributes.Tribe, bySlot, pet.Life),
            ComputeMaxMana(ki, levelRow, setNumber, bySlot, pet.Mana),
            ComputeAttackPower(strength, ki, levelRow, setNumber, bySlot),
            ComputeDefensePower(wisdom, levelRow, setNumber, bySlot),
            ComputeAttackSuccess(strength, levelRow, setNumber, bySlot),
            ComputeAttackBlock(wisdom, vitality, levelRow, setNumber, bySlot),
            ComputeCritical(setNumber, bySlot),
            ComputeCriticalDefence(setNumber, attributes.RebirthCount, attributes.Halo, bySlot),
            ComputeLuck(setNumber, bySlot),
            ComputeElementAttackPower(levelRow, setNumber, bySlot),
            ComputeElementDefensePower(setNumber, bySlot));
    }

    /// <summary>
    ///     The combat-ready "effective" stats: layers buffs, the "pet double" rule for ATK/DEF, and a handful
    ///     of set/title/cape adjustments on top of <see cref="ComputeBaseStats" />. MaxLife/MaxMana/CriticalDefence
    ///     pass through unchanged -- the legacy wrappers for those are pure cache reads with no buff math.
    /// </summary>
    /// <param name="buffs">Null/omitted = no buffs applied, matching aBuff being all-zero.</param>
    public static EffectiveStats ComputeEffectiveStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        BuffInfo? buffs = null,
        int legacySetNumber = 0,
        PetStatContribution pet = default)
    {
        var baseStats = ComputeBaseStats(attributes, equipment, levels, legacySetNumber, pet);
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.Tribe, equipment, legacySetNumber);
        var titleRank = attributes.Title % 100;

        var attackPower = ApplyBuffPercent(baseStats.AttackPower, GetBuffPercent(buffs, 0));
        attackPower = ApplyPetDoubleRule(attackPower, pet.AttackPower);
        attackPower += SetBonusTables.CapeIuBonus(bySlot[1], 1, 50f);

        var defensePower = ApplyBuffPercent(baseStats.DefensePower, GetBuffPercent(buffs, 1));
        defensePower = ApplyPetDoubleRule(defensePower, pet.DefensePower);
        defensePower += SetBonusTables.CapeIuBonus(bySlot[1], 2, 50f);

        var attackSuccess = ApplyBuffPercent(baseStats.AttackSuccess, GetBuffPercent(buffs, 2));
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 17));
        attackSuccess += SetBonusTables.GetWrapperAttackSuccessBonus(setNumber);

        var attackBlock = ApplyBuffPercent(baseStats.AttackBlock, GetBuffPercent(buffs, 3));
        attackBlock = ApplyBuffPercent(attackBlock, GetBuffPercent(buffs, 18));
        attackBlock += SetBonusTables.GetWrapperAttackBlockBonus(setNumber);

        var elementAttackPower = ApplyBuffPercent(baseStats.ElementAttackPower, GetBuffPercent(buffs, 4));
        elementAttackPower = (int)(elementAttackPower * (titleRank + 100) / 100f);
        elementAttackPower += SetBonusTables.GetWrapperElementAttackPowerBonus(setNumber);
        elementAttackPower += SetBonusTables.CapeIuBonus(bySlot[1], 5, 100f);

        var elementDefensePower = ApplyBuffPercent(baseStats.ElementDefensePower, GetBuffPercent(buffs, 5));
        elementDefensePower = (int)(elementDefensePower * (titleRank + 100) / 100f);
        elementDefensePower += SetBonusTables.CapeIuBonus(bySlot[1], 6, 100f);

        var critical = ApplyBuffPercent(baseStats.Critical, GetBuffPercent(buffs, 10));
        critical += RebirthCriticalWrapperBonus(attributes.RebirthCount);
        critical += SetBonusTables.GetWrapperCriticalBonus(setNumber);
        critical += SetBonusTables.CapeIuBonus(bySlot[1], 7, 0.5f);

        var luck = ApplyBuffPercent(baseStats.Luck, GetBuffPercent(buffs, 11));

        return baseStats with
        {
            AttackPower = attackPower,
            DefensePower = defensePower,
            AttackSuccess = attackSuccess,
            AttackBlock = attackBlock,
            Critical = critical,
            Luck = luck,
            ElementAttackPower = elementAttackPower,
            ElementDefensePower = elementDefensePower
        };
    }

    // ---- GetBaseVitality/Strength/Ki(Intelligence)/Wisdom(Dexterity) ----

    /// <summary>
    ///     stat = aHalo + rawStat + sum(13 slots) item stat + titleBonus. The aHalo term is not a typo --
    ///     it's added here on top of CriticalDefence's own separate halo/10 bonus; both are real, independent
    ///     uses of the same field.
    /// </summary>
    private static int ComputeVitality(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Vitality;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Vitality;
        return total + TitleVitalityBonus(attributes.Title);
    }

    private static int ComputeStrength(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Strength;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Strength;
        return total + TitleStrengthBonus(attributes.Title);
    }

    private static int ComputeKi(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Intelligence;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Intelligent;
        return total + TitleKiBonus(attributes.Title);
    }

    private static int ComputeWisdom(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot)
    {
        var total = attributes.Halo + attributes.Dexterity;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Dexterity;
        return total + TitleWisdomBonus(attributes.Title);
    }

    // ---- Title-rank bonus tranches (deliberately non-uniform if-nesting per stat) ----

    private static int TitleRankBonus(int[] table, int rank)
    {
        return rank is >= 1 and <= 14 ? table[rank - 1] : 0;
    }

    private static int TitleVitalityBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        // No <=200 test (101-300 both map to B) -- confirmed gap, not an omission.
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableC,
            _ => TitleTableB
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleStrengthBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableC,
            <= 300 => TitleTableD,
            <= 400 => TitleTableB,
            _ => TitleTableA
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleKiBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableA,
            _ => TitleTableC
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleWisdomBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        // No <=400 test (301-400 and >400 both map to D) -- confirmed gap, not an omission.
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableD,
            <= 300 => TitleTableE,
            _ => TitleTableD
        };
        return TitleRankBonus(table, rank);
    }

    // ---- GetBaseMaxLife ----

    private static int ComputeMaxLife(int vitality, LevelRowDto levelRow, int setNumber, bool isLegendarySet,
        byte tribe, EquippedItemSlot?[] bySlot, int petLife)
    {
        var hp = (int)(vitality * 20.0f);
        hp += levelRow.Life; // ornament/deco/elixir bonuses unmodeled, skipped
        hp = ApplyPetDoubleRule(hp, petLife); // HP-boost-pill/animal-grade/mount bonuses unmodeled

        hp += SetBonusTables.GetFlatLifeBonus(setNumber);
        hp += ComputeG12CustomSetBonus(tribe, bySlot);
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
            hp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

        return hp;
    }

    /// <summary>G12 custom-set bonus, gated on all 6 canonical slots being in-tribe IDs.</summary>
    private static int ComputeG12CustomSetBonus(byte tribe, EquippedItemSlot?[] bySlot)
    {
        var range = tribe switch
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

    private static int ComputeMaxMana(int ki, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot,
        int petMana)
    {
        var mp = (int)(ki * 15.3100004196167f); // exact source literal, not a rounded 15.31f
        mp += levelRow.Mana;
        mp = ApplyPetDoubleRule(mp, petMana);

        mp += SetBonusTables.GetFlatManaBonus(setNumber);
        mp += SetBonusTables.CapeIuBonus(bySlot[1], 4, 250f);

        if (bySlot[1] is { } cape)
            mp += cape.Item.ItemId switch { 1401 => 50, 1404 => 100, _ => 0 };

        if (bySlot[8] is { } petAmulet)
            mp += PhoenixFlatBonus(petAmulet.Item.ItemId, 2000, 4500, 9500);

        return mp;
    }

    // ---- GetBaseAttackPower ----

    private static int ComputeAttackPower(int strength, int ki, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
    {
        var weaponSlot = bySlot[7];
        var coefficients = ResolveWeaponAttackCoefficients(weaponSlot);
        // Two separate truncations for Str and Ki -- NOT (int)(str*fStr + ki*fKi).
        var atk = (int)(strength * coefficients.Str) + (int)(ki * coefficients.Ki);
        atk += levelRow.AttackPower;

        for (var i = 0; i < bySlot.Length; i++)
        {
            if (bySlot[i] is not { } slot) continue;
            atk += slot.Item.AttackPower;
            if (i != 8) // EPET: coefSet multiplier skipped, flat += above still applies
                atk += (int)(slot.Item.AttackPower *
                             SetBonusTables.GetCoefficients(setNumber, i, IsLegendary(slot.Item)).AttackPower);
        }

        if (bySlot[1] is { } capeSlot)
        {
            if (capeSlot.Item.Sort == 29) atk += 6 * capeSlot.Enchant;
            atk += capeSlot.Item.ItemId switch { 1404 => 1100, 1401 => 100, _ => 0 };
        }

        atk += ComputeWeaponAttackPowerBonus(weaponSlot);

        if (bySlot[10] is { } deco2)
            atk += ComputeDeco2AttackPowerBonus(deco2);
        // Deco 9/11/12 (and deco2's own sort==2 branch): ReturnNewStat unread, contributes 0 beyond the generic loop above.

        if (bySlot[8] is { } petAmulet)
        {
            atk -= petAmulet.Item
                .AttackPower; // Phoenix removes the item's own AttackPower stat, undoing the flat += above
            atk += PhoenixFlatBonus(petAmulet.Item.ItemId, 3000, 4000, 5000);
        }

        atk += SetBonusTables.GetBaseFlatAttackPowerBonus(setNumber); // NXT +500/1000/1500

        return atk;
    }

    private static (float Str, float Ki) ResolveWeaponAttackCoefficients(EquippedItemSlot? weapon)
    {
        // Values already include the MY_DMG_CAL +1.0f active in prod.
        return (weapon?.Item.Sort ?? 0) switch
        {
            13 or 17 or 19 => (3.65f, 2.43f),
            14 or 16 or 20 => (3.80f, 2.51f),
            15 or 18 or 21 => (3.51f, 2.35f),
            _ => (2.25f, 1.67f)
        };
    }

    private static int ComputeWeaponAttackPowerBonus(EquippedItemSlot? weaponSlot)
    {
        if (weaponSlot is not { } weapon) return 0;
        var item = weapon.Item;
        var total = 0;

        if (IsLegendary(item))
        {
            var enchant = (int)weapon.Enchant;
            if (enchant >= 100)
                enchant -= 100; // weapon uses >=100 (differs from the amulet's >100)
            total += enchant * 1200;
        }

        if (item.CheckSetItem == 2)
        {
            total += SetBonusTables.LinearByCombine(weapon.Combine, 400);
            var enchant = weapon.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)(item.AttackPower * enchant * 0.03f);
        }
        else if (item.CheckSetItem == 3)
        {
            total += SetBonusTables.WeaponIuSet3AttackPowerBonus(weapon.Enchant);
        }
        else
        {
            // Only ReturnIUEffectValue effect-sort 1 is faithfully reproducible -- see class remarks.
            var effect = WeaponAttackEffectValue(item);
            var e = effect * weapon.Combine;
            total += e;
            var enchant = weapon.Enchant;
            if (enchant is > 0 and <= 50)
                total += (int)((e + item.AttackPower) * enchant * 0.03f);
        }

        return total;
    }

    /// <summary>
    ///     ReturnIUEffectValue, effect-sort 1 (weapon attack): uses the weapon item's own Level column, not the
    ///     character's level.
    /// </summary>
    private static int WeaponAttackEffectValue(ItemRowDto weapon)
    {
        if (weapon.Sort != 4 && weapon.Sort is < 13 or > 21) return 0;

        var level = (float)weapon.Level;
        var (baseValue, pivot, slope) = level switch
        {
            < 100 => (0f, 45f, 0.10f),
            < 113 => (6f, 100f, 0.20f),
            _ => (8f, 113f, 0.50f) // < 146 always holds for real data (world.Levels is gapless 1..145)
        };

        return (int)(14.34f + (baseValue + (level - pivot) * slope) * 0.72f);
    }

    private static int ComputeDeco2AttackPowerBonus(EquippedItemSlot deco2)
    {
        if (deco2.Item.Sort == 2) return 0; // ReturnNewStat unread
        var isWing =
            deco2.Item.ItemId is 213 or 214 or 215 or 217 or 218 or 2303 or 2304 or 2305; // NOT 216, unlike DEF/EATK
        return (int)(deco2.Enchant * (isWing ? 23.4f : 11.7f));
    }

    // ---- GetBaseDefensePower ----

    private static int ComputeDefensePower(int wisdom, LevelRowDto levelRow, int setNumber, EquippedItemSlot?[] bySlot)
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
        if (deco2.Item.Sort == 2) return 0; // ReturnNewStat unread
        var isSpecial = deco2.Item.ItemId is 204 or 205 or 206 or 216 or 217 or 218 or 2303 or 2304 or 2305;
        return (int)(deco2.Enchant * (isSpecial ? 48.75f : 24.35f));
    }

    // ---- GetBaseAttackSuccess (HIT) ----

    private static int ComputeAttackSuccess(int strength, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
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

    private static int ComputeAttackBlock(int wisdom, int vitality, LevelRowDto levelRow, int setNumber,
        EquippedItemSlot?[] bySlot)
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
        // Deco 9-12 sort2: ReturnNewStat(6) unread, contributes 0 beyond the generic loop above.

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

    // ---- GetBaseCritical ----

    private static int ComputeCritical(int setNumber, EquippedItemSlot?[] bySlot)
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

        return crit;
    }

    // ---- GetBaseCriticalDefence ----

    private static int ComputeCriticalDefence(int setNumber, int rebirthCount, int halo, EquippedItemSlot?[] bySlot)
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

        return critDef;
    }

    // ---- GetBaseLuck ----

    private static int ComputeLuck(int setNumber, EquippedItemSlot?[] bySlot)
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

        return luck;
    }

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

    private static bool IsLegendary(ItemRowDto item)
    {
        return item.Sort is 1 or 4;
    }

    private static bool AnyLegendary(EquippedItemSlot?[] bySlot)
    {
        foreach (var slot in bySlot)
            if (slot is { } s && IsLegendary(s.Item))
                return true;
        return false;
    }

    private static int PhoenixFlatBonus(int itemId, int b76005, int b76006, int b76007)
    {
        return itemId switch { 76005 => b76005, 76006 => b76006, 76007 => b76007, _ => 0 };
    }

    private static EquippedItemSlot?[] BuildSlotLookup(IReadOnlyList<EquippedItemSlot> equipment)
    {
        var bySlot = new EquippedItemSlot?[13];
        foreach (var slot in equipment)
            if (slot.SlotIndex is >= 0 and < 13)
                bySlot[slot.SlotIndex] = slot;
        return bySlot;
    }

    /// <summary>
    ///     Levels 146-157 clamp down to the level-145 row; any level outside [1,157] is a zero-factor
    ///     contribution instead -- not a further clamp to 145.
    /// </summary>
    private static LevelRowDto GetLevelRow(FrozenDictionary<short, LevelRowDto> levels, short level)
    {
        if (level is < 1 or > 157) return ZeroLevelRow;
        var clamped = level > 145 ? (short)145 : level;
        return levels.TryGetValue(clamped, out var row) ? row : ZeroLevelRow;
    }

    private static int ApplyBuffPercent(int value, int? buffPercent)
    {
        return buffPercent is not { } pct || pct == 0 ? value : (int)(value * (pct + 100) * 0.01f);
    }

    /// <summary>Reads BUFF_INFO's flattened aBuff[slotIndex][0] (the percentage half of the pair).</summary>
    private static int? GetBuffPercent(BuffInfo? buffs, int slotIndex)
    {
        if (buffs is not { } b) return null;
        var idx = slotIndex * 2;
        return idx >= 0 && idx < b.Buff.Length ? b.Buff[idx] : null;
    }
}
