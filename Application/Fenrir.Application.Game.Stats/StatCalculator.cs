using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

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
///     Not implemented (each contributes 0, like a compiled-out legacy feature): the elixir event-tier/
///     official-guild override's tribe/guild-name resolution inputs (the override engine itself is live, see
///     <c>StatCalculator.FourGuildElixirContribution.cs</c>, but degrades to the raw per-elixir floor until
///     those inputs are threaded); zone038/FFA stat-override tables beyond the zone-335 MaxLife/MaxMana getter
///     fixes (Domain's <c>FfaEqualStatsOverride</c> owns the combat-stat side); the rage-buff attack multiplier
///     (dormant in the shipped build, see <c>StatCalculator.DrunkRageContribution.cs</c>); most of the
///     animal/mount system (Tier-1 grade multiplier and Tier-2 flat-per-point additive; only Tier-2b absorb is
///     live, see <see cref="MountAbsorbPrimaryBonus" />) and full PETSYSTEM beyond
///     <see cref="PetStatContribution" />'s Life/Mana/AttackPower/DefensePower/SteppedAttackBonus (the graded
///     IS/IU/IM ladders are built but unwired pending a runtime packed-graded-value field; the amulet flat
///     ATTACK/DEFENSE tables are wired into <see cref="ComputeAttackPower" />/<see cref="ComputeDefensePower" />
///     for their 6 non-Phoenix-overlapping ids only, see <c>StatCalculator.PetContribution.cs</c>); the
///     decoration ReturnNewStat IU/IM/IZ octets specifically --
///     WORKSTREAM B3-deco wired ReturnIUEffectValue effect-sorts 2-6 and the IS-only decoration contribution
///     (see <c>StatCalculator.DecoUpgradeContribution.cs</c>), but IU/IM/IZ stay unwired for decoration items
///     since no legacy citation supports them there (see <see cref="DecorationStatContribution" />'s remarks);
///     GIFT_EVENT amulet values,
///     mix-skill bonuses; legacy set-number detection for sets 1-22/30/50/51 (only NXT detection is
///     implemented, see <see cref="SetBonusTables" />); speeds (no GetSpeed exists in MyFactor at all).
///     <para>
///         Workstream B1 added the <see cref="CosmeticContext" />/<see cref="ZoneContext" />/
///         <see cref="ConsumableContext" />/<see cref="MountContext" /> optional-parameter surface (assembled
///         from <c>PlayerRuntimeState</c> by <c>EquipmentService.RecomputeStats</c>) so later workstreams can
///         feed the inputs above without breaking any caller/test. Workstream B2 then made the
///         <see cref="ConsumableContext" /> elixir counters LIVE: the strength-elixir feed to attack power, the
///         dexterity-elixir feed to accuracy/block, and the packed elemental-elixir feed to element
///         attack/defense are all consumed now, each zone-gated via <see cref="ZoneContext" />'s zone number.
///         Workstream B5 then made <see cref="CosmeticContext" />'s rune arrays LIVE: each rune socket's packed
///         stat feeds the four base attributes (Str/Dex/Vit/Ki) via
///         <see cref="StatCalculator.ComputeBaseStats" /> (see <see cref="RuneStatDecoder" />).
///     </para>
///     <para>
///         Wave-6 (workstreams B6/B7/B8) made most of the remaining context fields LIVE. BASE cache layer
///         (<see cref="ComputeBaseStats" />): <see cref="CosmeticContext" />'s costume value/enchant (four
///         primary attributes, Critical, Luck) and Stellar Core (AttackPower/DefensePower/CriticalDefence/
///         ElementAttack/ElementDefense/MaxLife); <see cref="ZoneContext" />'s ornament (MaxLife/MaxMana/
///         AttackPower/DefensePower) and the elixir override engine's raw-floor drop-in (behavior-identical
///         to B2 until its four-guild/event inputs land); the rank-buff HP tier (tier 6) and the two 878/880
///         drunk-potion CACHED legs (MaxLife -10%, CriticalDefence -10%); <see cref="MountContext" />'s
///         Tier-2b absorb add to the four primary attributes (contributes 0 in production until the
///         unrecoverable <c>MyAnimal</c> base table lands, see <see cref="MountAbsorbPrimaryBonus" />).
///         EFFECTIVE per-resolution layer (<see cref="ComputeEffectiveStats" />): the rank-buff tiers 1-5/7,
///         tribe-role's master/sub-master flat bonus (AttackPower/DefensePower/Critical), the five remaining
///         879/880/881/882 drunk-potion legs, and the pet stepped-attack bonus
///         (<see cref="PetStatContribution.SteppedAttackBonus" /> -- no Domain resolver populates it yet, so
///         it stays 0 until a caller wires <see cref="PetSteppedAttackBonus" /> in). Every drunk/rank-buff/
///         tribe-role/ornament/stellar/costume input is gated on <see cref="ZoneContext.DrunkStateId" />/
///         <see cref="ZoneContext.RankBuffType" />/<see cref="ZoneContext.TribeRole" />/
///         <see cref="ZoneContext.OrnamentInUse" />/<see cref="CosmeticContext.StellarCoreNumber" />/
///         <see cref="CosmeticContext.CostumeNumber" /> having a real <c>PlayerRuntimeState</c> source --
///         several (drunk state, ornament gold/silver time remaining, rage gauge, the costume-enchant packed
///         date) still have none, so those particular terms stay 0 in production despite the getter code
///         being live; see each context's own XML doc and this workstream's agent-memory notes for the exact
///         list. Each context's XML doc records which stat layer (base vs effective) its fields feed.
///     </para>
///     <para>
///         Workstream B13-socket-prerequisites made the one production-live gem-socket stat (AttackPower's
///         <c>GetSocketInfo(1,...)</c>, MyFactor.cpp:4031-4035) LIVE end to end: <see cref="EquippedItemSlot" />
///         now carries the packed <c>SOCKET_GEM_V2</c> blob, <c>WorldDataCache.GemSocketsByTypeAndValue</c>
///         supplies the (Type,Value02)-keyed effect table, and <see cref="ComputeAttackPower" /> folds
///         <c>SumGemSocketContribution</c> across every occupied slot inside its own equip-slot loop, matching
///         legacy's call-site placement. The seven sibling stat-type routings (Defense/MaxLife/MaxMana/
///         Attack-success/Attack-block/Elemental-attack/Elemental-defense) remain transcribed-but-unwired --
///         see <c>StatCalculator.GemSocketContribution.cs</c>'s <see cref="IsGemSocketStatLiveInProduction" />,
///         they are dead code in the only real build configuration (<c>USE_SOCKET_GEM</c> undefined) and wiring
///         any of them would be a new feature, not a parity fix.
///     </para>
/// </remarks>
public static partial class StatCalculator
{
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
    /// <param name="cosmetic">
    ///     Rune/costume/stellar BASE inputs (see class remarks). Rune, costume value/enchant, and Stellar Core
    ///     are all live; a default instance reproduces the equipment-only result.
    /// </param>
    /// <param name="zone">
    ///     Ornament/elixir-override/rank-buff-HP-tier/878+880-drunk BASE inputs (see class remarks). Rage and
    ///     the remaining rank-buff/drunk/tribe-role terms are EFFECTIVE-layer only, consumed by
    ///     <see cref="ComputeEffectiveStats" /> instead.
    /// </param>
    /// <param name="consumable">Potion-counter/pill BASE inputs -- the five Eat*Potion counters are live.</param>
    /// <param name="mount">Mount grade/absorb BASE inputs -- only Tier-2b absorb is live (see class remarks).</param>
    /// <param name="gemSocketsByTypeAndValue">
    ///     WORKSTREAM B13-socket-prerequisites: the (Type,Value02)-keyed <c>world.GemSockets</c> effect table
    ///     (<c>WorldDataCache.GemSocketsByTypeAndValue</c>) <see cref="ComputeAttackPower" /> folds each equipped
    ///     item's packed gem-socket blob against, via <c>SumGemSocketContribution</c>. Null (every caller that
    ///     predates this workstream) reproduces the pre-wiring, gem-socket-free result exactly -- this is a BASE
    ///     input; only AttackPower actually reads it (see class remarks / <c>StatCalculator.GemSocketContribution.cs</c>).
    /// </param>
    public static EffectiveStats ComputeBaseStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        int legacySetNumber = 0,
        PetStatContribution pet = default,
        CosmeticContext cosmetic = default,
        ZoneContext zone = default,
        ConsumableContext consumable = default,
        MountContext mount = default,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
    {
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
        var isLegendarySet = AnyLegendary(bySlot);
        // B12-rebirth-sum-recheck: MyFactor::GetLevel() is aLevel1+aLevel2, not aLevel1 alone -- every one of
        // the seven formulas fed by this single shared row (MaxLife/MaxMana/AttackPower/DefensePower/
        // AttackSuccess/AttackBlock/ElementAttackPower) reads the combined value (MyFactor.cpp:508-511 plus
        // each formula's own level-factor call site). See CharacterBaseAttributes.CombinedLevel's own remarks
        // for why GetLevelRow's existing [1,157] range already anticipated this.
        var levelRow = GetLevelRow(levels, attributes.CombinedLevel);

        var vitality = ComputeVitality(attributes, bySlot, cosmetic, consumable, mount);
        var strength = ComputeStrength(attributes, bySlot, cosmetic, consumable, mount);
        var ki = ComputeKi(attributes, bySlot, cosmetic, consumable, mount);
        var wisdom = ComputeWisdom(attributes, bySlot, cosmetic, consumable, mount);

        return new EffectiveStats(
            ComputeMaxLife(vitality, levelRow, setNumber, isLegendarySet, attributes.PreviousTribe, bySlot, pet.Life,
                zone, consumable, mount, cosmetic),
            ComputeMaxMana(ki, levelRow, setNumber, bySlot, pet.Mana, zone, consumable, mount),
            ComputeAttackPower(strength, ki, levelRow, setNumber, bySlot, cosmetic, zone, mount, consumable,
                gemSocketsByTypeAndValue),
            ComputeDefensePower(wisdom, levelRow, setNumber, bySlot, cosmetic, zone, mount),
            ComputeAttackSuccess(strength, levelRow, setNumber, bySlot, mount, zone, consumable),
            ComputeAttackBlock(wisdom, vitality, levelRow, setNumber, bySlot, mount, zone, consumable),
            ComputeCritical(setNumber, bySlot, cosmetic, mount),
            ComputeCriticalDefence(setNumber, attributes.RebirthCount, attributes.Halo, bySlot, cosmetic, mount, zone),
            ComputeLuck(setNumber, bySlot, cosmetic),
            ComputeElementAttackPower(levelRow, setNumber, bySlot, cosmetic, consumable, mount, zone),
            ComputeElementDefensePower(setNumber, bySlot, cosmetic, consumable, mount, zone));
    }

    /// <summary>
    ///     The combat-ready "effective" stats: layers buffs, the "pet double" rule for ATK/DEF, and a handful
    ///     of set/title/cape adjustments on top of <see cref="ComputeBaseStats" />. MaxLife/MaxMana/CriticalDefence
    ///     pass through unchanged -- the legacy wrappers for those are pure cache reads with no buff math.
    /// </summary>
    /// <param name="buffs">Null/omitted = no buffs applied, matching aBuff being all-zero.</param>
    /// <param name="cosmetic">
    ///     Rune/costume/stellar BASE inputs, forwarded to <see cref="ComputeBaseStats" /> (see class remarks).
    ///     Default reproduces the equipment-only result.
    /// </param>
    /// <param name="zone">
    ///     Ornament/elixir-override (BASE, forwarded) and tribe-role/rank-buff/drunk (EFFECTIVE, consumed
    ///     directly here) inputs -- rage remains an inert seam (see class remarks).
    /// </param>
    /// <param name="consumable">Potion-counter/pill BASE inputs, forwarded to <see cref="ComputeBaseStats" />.</param>
    /// <param name="mount">
    ///     Mount grade/absorb (BASE, forwarded -- only Tier-2b absorb is live) and runtime-attribute (EFFECTIVE)
    ///     inputs; the runtime-attribute half remains an inert seam.
    /// </param>
    /// <param name="gemSocketsByTypeAndValue">
    ///     BASE input, forwarded to <see cref="ComputeBaseStats" /> -- see that method's own parameter doc.
    /// </param>
    public static EffectiveStats ComputeEffectiveStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        BuffInfo? buffs = null,
        int legacySetNumber = 0,
        PetStatContribution pet = default,
        CosmeticContext cosmetic = default,
        ZoneContext zone = default,
        ConsumableContext consumable = default,
        MountContext mount = default,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
    {
        var baseStats = ComputeBaseStats(attributes, equipment, levels, legacySetNumber, pet, cosmetic, zone,
            consumable, mount, gemSocketsByTypeAndValue);
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
        var titleRank = attributes.Title % 100;

        // B7 drunk-rage 878/879: multiplies the CACHED base value first (legacy order: after the unmodelled
        // mix-skill 5% bonus, before the unmodelled tribe-role flat bonus), then buffs layer on top as before.
        var attackPower = ApplyDrunkAttackPower(baseStats.AttackPower, zone);
        attackPower = ApplyBuffPercent(attackPower, GetBuffPercent(buffs, 0));
        attackPower = ApplyPetDoubleRule(attackPower, pet.AttackPower);
        attackPower += pet.SteppedAttackBonus; // B8 pet stepped-attack bonus (co-located with the pet-double rule)
        attackPower += SetBonusTables.CapeIuBonus(bySlot[1], 1, 50f);
        attackPower += RankBuffAttackPowerBonus(zone); // B7 rank-buff tier 7 (+500, the sole non-1000 magnitude)
        attackPower += TribeRoleAttackPowerBonus(zone); // B7 tribe-role master/sub-master (+200/+100)

        // B7 drunk-rage 879: doubles the CACHED base value first, then buffs layer on top.
        var defensePower = ApplyDrunkDefensePower(baseStats.DefensePower, zone);
        defensePower = ApplyBuffPercent(defensePower, GetBuffPercent(buffs, 1));
        defensePower = ApplyPetDoubleRule(defensePower, pet.DefensePower);
        defensePower += SetBonusTables.CapeIuBonus(bySlot[1], 2, 50f);
        defensePower += RankBuffDefensePowerBonus(zone); // B7 rank-buff tier 1 (+1000)
        defensePower += TribeRoleDefensePowerBonus(zone); // B7 tribe-role master/sub-master (+200/+100)

        // B7 drunk-rage 881: -20% hit, applied to the CACHED base value first, then buffs layer on top.
        var attackSuccess = ApplyDrunkAttackSuccess(baseStats.AttackSuccess, zone);
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 2));
        attackSuccess = ApplyBuffPercent(attackSuccess, GetBuffPercent(buffs, 17));
        attackSuccess += SetBonusTables.GetWrapperAttackSuccessBonus(setNumber);
        attackSuccess += RankBuffAttackSuccessBonus(zone); // B7 rank-buff tier 5 (+1000)

        var attackBlock = ApplyBuffPercent(baseStats.AttackBlock, GetBuffPercent(buffs, 3));
        attackBlock = ApplyBuffPercent(attackBlock, GetBuffPercent(buffs, 18));
        attackBlock += SetBonusTables.GetWrapperAttackBlockBonus(setNumber);
        attackBlock += RankBuffAttackBlockBonus(zone); // B7 rank-buff tier 4 (+1000)

        // B7 drunk-rage 882: +30% element attack, applied to the CACHED base value first, then buffs layer on top.
        var elementAttackPower = ApplyDrunkElementAttack(baseStats.ElementAttackPower, zone);
        elementAttackPower = ApplyBuffPercent(elementAttackPower, GetBuffPercent(buffs, 4));
        elementAttackPower = (int)(elementAttackPower * (titleRank + 100) / 100f);
        elementAttackPower += SetBonusTables.GetWrapperElementAttackPowerBonus(setNumber);
        elementAttackPower += SetBonusTables.CapeIuBonus(bySlot[1], 5, 100f);
        elementAttackPower += RankBuffElementAttackPowerBonus(zone); // B7 rank-buff tier 3 (+1000)

        // B7 drunk-rage 882: -50% element defense, applied to the CACHED base value first, then buffs layer on top.
        var elementDefensePower = ApplyDrunkElementDefense(baseStats.ElementDefensePower, zone);
        elementDefensePower = ApplyBuffPercent(elementDefensePower, GetBuffPercent(buffs, 5));
        elementDefensePower = (int)(elementDefensePower * (titleRank + 100) / 100f);
        elementDefensePower += SetBonusTables.CapeIuBonus(bySlot[1], 6, 100f);
        elementDefensePower += RankBuffElementDefensePowerBonus(zone); // B7 rank-buff tier 2 (+1000)

        // B7 drunk-rage 880: +5% critical, applied to the CACHED base value first, then buffs layer on top.
        var critical = ApplyDrunkCritical(baseStats.Critical, zone);
        critical = ApplyBuffPercent(critical, GetBuffPercent(buffs, 10));
        critical += RebirthCriticalWrapperBonus(attributes.RebirthCount);
        critical += SetBonusTables.GetWrapperCriticalBonus(setNumber);
        critical += SetBonusTables.CapeIuBonus(bySlot[1], 7, 0.5f);
        critical += TribeRoleCriticalBonus(zone); // B7 tribe-role master ONLY (+2, no sub-master bonus)

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

    // ---- shared helpers ----

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
