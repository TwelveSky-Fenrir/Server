namespace Fenrir.Application.Game.Stats;

/// <summary>
///     One occupied equipment slot: the world.Items template plus the 4 legacy "upgrade bytes" (Enchant/"+N",
///     Combine/"CS", Refine, Socket). <see cref="SlotIndex" /> is the raw legacy equipment-array index (0-12).
///     The legacy FEQUIP_TYPE enum's names are inverted vs. their real semantics (index 0 is "Ring" not
///     EAMULET, index 4 is "Amulet" not ERING) -- every formula below keys off the bare index, never a name.
///     Refine/Socket are carried but never read: USE_REFINE/USE_SOCKET_GEM are both undefined in the M33 prod
///     config, so those legacy code paths are dead here too.
/// </summary>
public readonly record struct EquippedItemSlot(
    int SlotIndex,
    ItemRowDto Item,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket);

/// <summary>
///     The 4 spent base stats plus the progression fields MyFactor's base-stat formulas read. Field names
///     follow the real stat identity (Vitality/Strength/Intelligence/Dexterity) rather than MyFactor's
///     internal names -- "Ki" in the legacy IS Intelligence and "Wisdom" IS Dexterity (GetBaseKi() reads
///     iIntelligent, GetBaseWisdom() reads iDexterity).
/// </summary>
public readonly record struct CharacterBaseAttributes(
    int Vitality,
    int Strength,
    int Intelligence,
    int Dexterity,
    short Level,
    byte Tribe,
    int Title,
    int Halo,
    int RebirthCount);

/// <summary>
///     The pet's own Life/Mana/AttackPower/DefensePower contribution to the "pet double" rule: if stat &lt;
///     stat_pet, the running stat is doubled instead of added to. Default (all-zero) reduces
///     <see cref="StatCalculator.ApplyPetDoubleRule" /> to a no-op for callers with no pet integration.
/// </summary>
public readonly record struct PetStatContribution(
    int Life = 0,
    int Mana = 0,
    int AttackPower = 0,
    int DefensePower = 0);

/// <summary>
///     One MyFactor stat snapshot -- shared shape for the cached "base" layer
///     (<see cref="StatCalculator.ComputeBaseStats" />) and the combat-ready "effective" layer
///     (<see cref="StatCalculator.ComputeEffectiveStats" />). MaxLife/MaxMana/CriticalDefence are pure cache
///     reads in the legacy (no wrapper math), so they're identical between the two layers here too.
///     AttackPower is a single scalar (MyFactor has no min/max damage range concept); the actual damage roll
///     is computed downstream and is out of scope here.
/// </summary>
public readonly record struct EffectiveStats(
    int MaxLife,
    int MaxMana,
    int AttackPower,
    int DefensePower,
    int AttackSuccess,
    int AttackBlock,
    int Critical,
    int CriticalDefence,
    int Luck,
    int ElementAttackPower,
    int ElementDefensePower);
