using System.Collections.Frozen;
using Fenrir.Network.Serialization.Packets.Shared;

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
    public static EffectiveStats ComputeBaseStats(
        CharacterBaseAttributes attributes,
        IReadOnlyList<EquippedItemSlot> equipment,
        FrozenDictionary<short, LevelRowDto> levels,
        int legacySetNumber = 0,
        PetStatContribution pet = default)
    {
        var bySlot = BuildSlotLookup(equipment);
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
        var isLegendarySet = AnyLegendary(bySlot);
        var levelRow = GetLevelRow(levels, attributes.Level);

        var vitality = ComputeVitality(attributes, bySlot);
        var strength = ComputeStrength(attributes, bySlot);
        var ki = ComputeKi(attributes, bySlot);
        var wisdom = ComputeWisdom(attributes, bySlot);

        return new EffectiveStats(
            ComputeMaxLife(vitality, levelRow, setNumber, isLegendarySet, attributes.PreviousTribe, bySlot, pet.Life),
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
        var setNumber = SetBonusTables.ResolveEffectiveSetNumber(attributes.PreviousTribe, equipment, legacySetNumber);
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
