using Fenrir.Data.World;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     One occupied equipment slot as <see cref="StatCalculator" /> needs it: the world.Items template plus
///     the 4 legacy "upgrade bytes" the client/server carry per item (report 11 §2 -- octet0=IS/Enchant =
///     improve/enchant level a.k.a. "+N", octet1=IU/Combine = combine value a.k.a. "CS", octet2=IM/Refine =
///     refine level, octet3=IZ/Socket = socket index). Fenrir already stores these as 4 separate TINYINT
///     columns (game.CharacterItems.Enchant/Combine/Refine/Socket) instead of the legacy packed int, so no
///     bit-unpacking happens here -- this type is just that row joined with its world.Items template.
///     <see cref="SlotIndex" /> is the raw legacy equipment-array index (0-12, MAX_EQUIP_SLOT_NUM=13).
///     Report 11 §0 documents a real naming inversion in the legacy FEQUIP_TYPE enum vs. the code comments
///     that actually describe semantics (index 0 is commented "Ring", enum calls it EAMULET; index 4 is
///     commented "Amulet", enum calls it ERING) -- per the report's own guidance ("se fier aux indices, pas
///     aux noms"), every formula below keys off the bare index, never a slot name.
///     Refine is carried but never read by any formula: USE_REFINE is not defined under the M33 prod config
///     (report §0), so every legacy Return*Up(refine) call site is dead code in this build. Socket is
///     carried but also unused: USE_SOCKET_GEM is undefined too, and the one still-active call site
///     (GetSocketInfo inside GetAttackPower, report §10.5) reads a function whose body was never located in
///     the source pass -- assumed to contribute 0 with no gems equipped (a documented lacuna, not invented).
/// </summary>
public readonly record struct EquippedItemSlot(
    int SlotIndex,
    ItemRowDto Item,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket);

/// <summary>
///     The 4 spent base stats plus the character-progression fields MyFactor's base-stat formulas read
///     (report 11 §3-§5.5). Field names follow the REAL stat identity (Vitality/Strength/Intelligence/
///     Dexterity, matching game.Characters.StatVit/StatStr/StatInt/StatDex) rather than MyFactor's internal
///     variable names -- the report's "Ki" IS this Intelligence stat and its "Wisdom" IS this Dexterity
///     stat (GetBaseKi() reads iIntelligent, GetBaseWisdom() reads iDexterity); every formula method that
///     consumes one of these cites which legacy name it corresponds to.
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
///     The pet's own Life/Mana/AttackPower/DefensePower contribution to the "pet double" rule (report 11
///     §5.1 step 10, §5.2, §6 GetAttackPower/GetDefensePower rows, and §11: "si stat &lt; stat_pet -&gt; ×2
///     au lieu de +"). The actual PETSYSTEM formulas (ReturnLifeValue/ReturnManaValue/ReturnAttackPower/
///     ReturnDefensePower) live in GameSystem_07_Pet.cpp, never read in report 11 (§10.3 lacuna) -- a
///     caller with no pet-system integration yet passes the default (all-zero), which reduces every
///     <see cref="StatCalculator.ApplyPetDoubleRule" /> call to a no-op "+= 0" (since stat &gt;= 0 is always
///     true for the stats this rule applies to). This default has NOT been verified against the real
///     ReturnLifeValue-with-no-pet-equipped return value -- flagged as an open issue, not guessed.
/// </summary>
public readonly record struct PetStatContribution(
    int Life = 0,
    int Mana = 0,
    int AttackPower = 0,
    int DefensePower = 0);

/// <summary>
///     One MyFactor stat snapshot -- the shape shared by both the cached "base" layer
///     (SetBasicAbilityFromEquip, report §1/§5, produced by <see cref="StatCalculator.ComputeBaseStats" />)
///     and the final combat-ready "effective" layer (the Get* wrappers, report §6, produced by
///     <see cref="StatCalculator.ComputeEffectiveStats" />). GetMaxLife/GetMaxMana/GetCriticalDefense are
///     pure cache reads in the legacy (no wrapper math) -- MaxLife, MaxMana and CriticalDefence are
///     therefore identical between the two layers here too.
///     AttackPower is a single scalar, matching GetAttackPower's single return value -- MyFactor has no
///     concept of a min/max damage range. The actual damage ROLL (the min/max variance a combat log would
///     show) is computed downstream in S07_MyGame02.cpp, which report 11 never reads. Do not add a
///     min/max split here without a verified source for it (see mission's "ne l'invente pas").
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
