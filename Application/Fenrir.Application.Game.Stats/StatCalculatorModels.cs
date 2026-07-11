namespace Fenrir.Application.Game.Stats;

/// <summary>
///     One occupied equipment slot: the world.Items template plus the 4 legacy "upgrade bytes" (Enchant/"+N",
///     Combine/"CS", Refine, Socket) and the packed <c>SOCKET_GEM_V2</c> gem-socket blob (<see cref="SocketGem1" />/
///     <see cref="SocketGem2" />/<see cref="SocketGem3" />). <see cref="SlotIndex" /> is the raw legacy
///     equipment-array index (0-12). The legacy FEQUIP_TYPE enum's names are inverted vs. their real semantics
///     (index 0 is "Ring" not EAMULET, index 4 is "Amulet" not ERING) -- every formula below keys off the bare
///     index, never a name. Refine is carried but never read: USE_REFINE is undefined in the M33 prod config, so
///     that legacy code path is dead here too. <see cref="Socket" /> (the single legacy upgrade byte) is likewise
///     never read directly by any formula.
/// </summary>
/// <param name="SocketGem1">
///     Word 1 of 3 of the packed <c>SOCKET_GEM_V2</c> blob (<c>game.CharacterItems.SocketGem1</c> for the
///     Container=2 equipment rows, same shape as <c>ItemStack.SocketGem1</c>) -- an unused leading byte plus
///     the active-gem-count byte. Together with
///     <see cref="SocketGem2" />/<see cref="SocketGem3" /> this feeds <see cref="StatCalculator.ComputeAttackPower" />
///     via <c>StatCalculator.SumGemSocketContribution</c> (the one gem-socket call site not gated by
///     <c>USE_SOCKET_GEM</c>, Server/Header/Protocol/MyFactor.cpp:4031-4035) -- see
///     <c>StatCalculator.GemSocketContribution.cs</c> for the decode/routing. Defaults to 0 (no sockets), which
///     reproduces the pre-wiring, gem-socket-free result for any caller that does not populate it.
/// </param>
/// <param name="SocketGem2">Word 2 of 3 -- see <see cref="SocketGem1" />.</param>
/// <param name="SocketGem3">Word 3 of 3 -- see <see cref="SocketGem1" />.</param>
public readonly record struct EquippedItemSlot(
    int SlotIndex,
    ItemRowDto Item,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1 = 0,
    int SocketGem2 = 0,
    int SocketGem3 = 0);

/// <summary>
///     The 4 spent base stats plus the progression fields MyFactor's base-stat formulas read. Field names
///     follow the real stat identity (Vitality/Strength/Intelligence/Dexterity) rather than MyFactor's
///     internal names -- "Ki" in the legacy IS Intelligence and "Wisdom" IS Dexterity (GetBaseKi() reads
///     iIntelligent, GetBaseWisdom() reads iDexterity).
/// </summary>
/// <param name="Tribe">The character's current playable faction (0-3). Not itself read by any formula below.</param>
/// <param name="PreviousTribe">
///     aPreviousTribe -- the character's origin-race marker (0=Noble Dragon, 1=Royal Serpent, 2=Grand
///     Tiger). For the overwhelming majority of characters this equals <see cref="Tribe" />; it only
///     genuinely diverges for a fourth-faction (<see cref="Tribe" />==3) character, whose origin race is
///     preserved here. The G12 custom-set HP bonus (<see cref="StatCalculator.ComputeBaseStats" /> via
///     <c>ComputeG12CustomSetBonus</c>) and the NXT set-tier detector
///     (<see cref="SetBonusTables.DetectNxtSetNumber" />) both key off this field, never <see cref="Tribe" />
///     -- Server/Header/Protocol/MyFactor.cpp:2032-2094, Server/ts25zone/S07_MyGame03.cpp:7516-7626.
/// </param>
/// <param name="Level2">
///     aLevel2 -- the post-general-level-cap "high level"/rebirth-tier ladder (0-12,
///     <c>PlayerRuntimeState.Level2</c>). Defaults to 0 (no rebirth tier yet), which makes
///     <see cref="CombinedLevel" /> equal <see cref="Level" /> alone -- the pre-B12-rebirth-sum-recheck
///     behavior, unchanged for every caller that has not been threaded with a real value yet. See
///     <see cref="CombinedLevel" /> for the formula this feeds.
/// </param>
public readonly record struct CharacterBaseAttributes(
    int Vitality,
    int Strength,
    int Intelligence,
    int Dexterity,
    short Level,
    byte Tribe,
    byte PreviousTribe,
    int Title,
    int Halo,
    int RebirthCount,
    short Level2 = 0)
{
    /// <summary>
    ///     aLevel1+aLevel2 -- <c>MyFactor::GetLevel()</c>, the level-factor-table lookup input for max life/
    ///     mana/attack power/defense power/attack success/attack block/elemental attack power (all seven
    ///     read the same combined value via <see cref="StatCalculator.ComputeBaseStats" />'s single shared
    ///     <c>levelRow</c>). A combined value of 1-157 is exactly the range <see cref="StatCalculator" />'s
    ///     own level-row lookup already clamps/zeroes against (145 general cap + 12 rebirth-tier cap = 157) --
    ///     confirming this lookup was already shaped for the combined value even before any caller supplied a
    ///     non-zero <see cref="Level2" />.
    /// </summary>
    /// <remarks>Réf. C++ : Server/Header/Protocol/MyFactor.cpp:508-511 (GetLevel()).</remarks>
    public short CombinedLevel => (short)(Level + Level2);
}

/// <summary>
///     The pet's own Life/Mana/AttackPower/DefensePower contribution to the "pet double" rule: if stat &lt;
///     stat_pet, the running stat is doubled instead of added to. Default (all-zero) reduces
///     <see cref="StatCalculator.ApplyPetDoubleRule" /> to a no-op for callers with no pet integration.
/// </summary>
/// <param name="SteppedAttackBonus">
///     WORKSTREAM B8 (Wave-6): the growth-pet stepped attack addend (<c>PETSYSTEM::ReturnAttackPower2</c>,
///     see <see cref="StatCalculator.PetSteppedAttackBonus" />), consumed flat in
///     <see cref="StatCalculator.ComputeEffectiveStats" /> right after the pet-double rule. No Domain
///     resolver populates this yet (the caller must set it from <c>PetGrowthCalculator</c>'s own tier/family
///     resolution) -- defaults to 0, which reproduces today's equipment-only result.
/// </param>
public readonly record struct PetStatContribution(
    int Life = 0,
    int Mana = 0,
    int AttackPower = 0,
    int DefensePower = 0,
    int SteppedAttackBonus = 0);

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
