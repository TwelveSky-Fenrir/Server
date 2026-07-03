using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.World;

/// <summary>
///     A player's in-memory, authoritative state while <c>InWorld</c>. Mutated ONLY by <see cref="Zone.RunAsync" />
///     (architecture reference §10.1: "un seul écrivain") — every other thread that needs a player's state posts a
///     <see cref="ZoneCommand" /> and waits for the next tick instead of touching this directly.
/// </summary>
public sealed class PlayerRuntimeState
{
    public required int CharacterId { get; init; }
    public required IPacketSession Session { get; init; }
    public required string Name { get; init; }
    public required byte Tribe { get; init; }
    public required byte Gender { get; init; }
    public required byte HeadType { get; init; }
    public required byte FaceType { get; init; }
    public required short Level { get; init; }

    public short MapId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }

    /// <summary>
    ///     Spent base stat points (game.Characters.StatVit/StatStr/StatInt/StatDex, A3/A4 migration; legacy
    ///     aVit/aStr/aInt/aDex). Feed <see cref="StatCalculator.ComputeBaseStats" />/
    ///     <see cref="StatCalculator.ComputeEffectiveStats" /> directly as
    ///     <see cref="CharacterBaseAttributes" />.Vitality/Strength/Intelligence/Dexterity (report 11 §3) —
    ///     StatInt IS the report's "Ki", StatDex IS the report's "Wisdom", see that type's remarks.
    /// </summary>
    public int StatVit { get; set; }

    public int StatStr { get; set; }
    public int StatInt { get; set; }
    public int StatDex { get; set; }

    /// <summary>Unspent stat/skill points (aStatPoint/aSkillPoint) — spend-on-levelup UI reads these, not StatCalculator.</summary>
    public int StatPoints { get; set; }

    public int SkillPoints { get; set; }

    /// <summary>Total XP (aExp1/aExp2 combined, game.Characters.Experience — see that column's migration comment).</summary>
    public long Experience { get; set; }

    /// <summary>
    ///     aRebirthNum (MAX_REBIRTH_LIMIT=12) — read by StatCalculator's CriticalDefence base bonus (report §5.5)
    ///     and Critical wrapper bonus (report §6), both via <see cref="CharacterBaseAttributes.RebirthCount" />.
    /// </summary>
    public int RebirthCount { get; set; }

    /// <summary>
    ///     aTitle (category*100 + rank 1-14) — read by StatCalculator's title-rank bonus tables (report §4) and
    ///     the ElementAttackPower/ElementDefensePower wrapper's rank multiplier (report §6).
    /// </summary>
    public int Title { get; set; }

    /// <summary>
    ///     aHalo — read by StatCalculator TWICE, independently: added directly to all 4 base stats (report §3)
    ///     AND its own separate halo/10 (or +10 at exactly 96) CriticalDefence base bonus (report §5.5).
    /// </summary>
    public int Halo { get; set; }

    /// <summary>aKillOtherTribe (CP) — quest reward type 3 income (report 04 §5); not consumed by StatCalculator.</summary>
    public int ContributionPoints { get; set; }

    /// <summary>
    ///     Cached output of <see cref="StatCalculator.ComputeEffectiveStats" /> — null until first computed.
    ///     Recompute ONLY on an equipment/buff/level/title/halo CHANGE EVENT (equip, unequip, enchant, level-up,
    ///     buff apply/expire, rebirth...), never once per tick: this mirrors the legacy's own
    ///     SetBasicAbilityFromEquip cache-on-write model (report 11 §1), not the per-tick simulation cost every
    ///     other <see cref="PlayerRuntimeState" /> field pays.
    ///     Refreshed by <see cref="Zone" />'s own tick: seeded from the world-entry snapshot at
    ///     <c>Zone.HandleEnter</c> (computed by <c>EnterWorldHandler</c> from the persisted
    ///     equipment, per <see cref="Fenrir.Application.Game.Inventory.EquipmentService.RecomputeStats" />), and replaced wholesale by
    ///     <c>Zone.ApplyInventoryCommand</c> whenever an accepted move touches
    ///     <see cref="Fenrir.Application.Game.Inventory.ContainerMatrix.Equipment" /> (<c>GenericActionHandler</c>'s own
    ///     precomputed <see cref="Fenrir.Application.Game.Inventory.InventoryZoneCommand.UpdatedStats" />).
    /// </summary>
    public EffectiveStats? Stats { get; set; }

    /// <summary>
    ///     This character's item containers (inventory pages, equipment, store pages) while <c>InWorld</c> --
    ///     see <see cref="Fenrir.Application.Game.Inventory.InventoryState" />'s own remarks for the single-writer contract (identical
    ///     to every other field on this type: mutated ONLY by <see cref="Zone" />'s own tick). Seeded at world
    ///     entry from the A3 world-entry bundle's item result set (<c>Zone.HandleEnter</c>), kept current by
    ///     <c>Zone.ApplyInventoryCommand</c> for every later accepted container move/equip/unequip
    ///     (<c>GenericActionHandler</c>). A plain field-initialized instance (not <c>required</c>): unlike
    ///     the scalar vitals above, an EMPTY inventory is a perfectly valid starting state (a brand-new
    ///     character), so there is no "caller forgot to set this" failure mode to guard against here.
    /// </summary>
    public InventoryState Inventory { get; } = new();

    /// <summary>
    ///     Server-side monotonic counter, independent of the DB's own FlushSequence baseline — incremented once per
    ///     accepted move, never reset, so usp_Character_PersistBatch's idempotence guard (§12.6) always sees a strictly
    ///     increasing value for this character's lifetime in this zone.
    /// </summary>
    public long FlushSequence { get; set; }

    /// <summary>
    ///     AOI grid bookkeeping — which cell this player currently occupies, so <see cref="AoiGrid" /> can detect a
    ///     crossing without a full rescan.
    /// </summary>
    public (int X, int Z) CurrentCell { get; set; }

    /// <summary>
    ///     When the last ACCEPTED move was applied — <see cref="Movement.MovementRules" /> measures elapsed time from
    ///     this, not from the tick's own clock, so a client that skips ticks isn't penalized for the server's schedule.
    /// </summary>
    public DateTime LastMoveUtc { get; set; }

    /// <summary>
    ///     Zone-clock instant (the zone's own simulated <c>TimeSpan</c> clock, not wall time) of the last
    ///     keep-alive rebroadcast of this avatar to its AOI neighbors — the 3.5 s legacy cadence
    ///     (<see cref="Simulation.LegacyTime.AvatarRebroadcastInterval" />) is measured from this. Stamped at
    ///     Enter, refreshed only by the rebroadcast itself (a keep-alive, exactly like the legacy
    ///     <c>tLogicAvatarTick</c> — moving does NOT reset it).
    /// </summary>
    public TimeSpan LastAvatarRebroadcastAt { get; set; }

    /// <summary>
    ///     Stable per-object wire identifier for <c>ZC_AVATAR_ACTION_RECV.UniqueNumber</c>. The wire contract
    ///     names this field but does not resolve its exact semantics (§5.7 lists it as an unelaborated DWORD) —
    ///     CharacterId is reused here since it is already unique and stable for the object's whole lifetime in
    ///     the zone, a reasonable, documented M1 stand-in absent a more specific legacy definition.
    /// </summary>
    public uint UniqueNumber => unchecked((uint)CharacterId);

    /// <summary>
    ///     True from <see cref="Zone.ApplyDeath" /> until the scheduled automatic revive fires (report 12 §4.2:
    ///     ~5 s later, "mCheckDeath"). Not yet consumed by movement/combat gating in this pass — no handler
    ///     currently checks it (the legacy blocks potions/most interactions while <c>aAction.aSort</c> is 11
    ///     stun/12 death); left for the Phase C/V3 combat handler to read, exactly like <see cref="Life" /> and
    ///     the other vitals it will also need.
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    ///     Zone-clock instant (<see cref="LastAvatarRebroadcastAt" />'s own convention: this zone's simulated
    ///     <c>TimeSpan</c> clock, not wall time) at which <see cref="Zone.ApplyDeath" />'s scheduled auto-revive
    ///     fires. Meaningless while <see cref="IsDead" /> is false. The revive is always IN PLACE (report 12
    ///     §4.2/§4.3: the legacy only auto-clears the death flag locally after the delay, never teleports) --
    ///     no destination zone/position needs to be carried alongside this timestamp.
    /// </summary>
    public TimeSpan ReviveAtZoneClock { get; set; }

    /// <summary>
    ///     Mirrors the legacy's own persistent <c>mDATA.aAction.aSort</c> (report 05 §7/§12 §4.2) -- the last
    ///     accepted avatar action's Sort, updated by <see cref="Zone.HandleMove" /> alongside position for
    ///     EVERY action, not just movement (the wire carries one unified action for move/sit/skill-cast alike).
    ///     Read by <see cref="Simulation.MeditationRegenSystem" /> (31 = sitting/meditating) and by
    ///     <c>Zone.ApplySkillCast</c>'s own gating. 0 = idle, matching the legacy default.
    /// </summary>
    public int ActionSort { get; set; }

    /// <summary>
    ///     The skill number/grade points riding on the last accepted action (<c>ActionInfo.SkillNumber</c>/
    ///     <c>SkillGradeNum1/2</c>) -- kept live (not reset to 0 between ticks, matching the legacy's own
    ///     persistent <c>mDATA.aAction</c>) so <see cref="Simulation.MeditationRegenSystem" /> can resolve
    ///     "which sit-skill is this player using" every legacy tick without the client having to resend it.
    /// </summary>
    public int ActionSkillNumber { get; set; }

    public int ActionSkillGradeNum1 { get; set; }
    public int ActionSkillGradeNum2 { get; set; }

    /// <summary>
    ///     Live BUFF_INFO mirror (35 slots x [value, duration-in-legacy-ticks], report 12 §4.2 / report §7
    ///     point 4) -- fed to <see cref="Stats.StatCalculator.ComputeEffectiveStats" /> as the buff snapshot and
    ///     decremented/expired by <see cref="Simulation.BuffExpirySystem" /> every legacy tick.
    /// </summary>
    /// <remarks>
    ///     Deliberately a FRESH per-instance array (never <c>Fenrir.Contracts.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
    ///     which is one process-wide SHARED static instance) -- reusing that template here would let every
    ///     player's buffs alias the same backing <c>int[]</c> and corrupt each other.
    /// </remarks>
    public BuffInfo Buffs { get; } = new() { Buff = new int[70] };

    /// <summary>
    ///     Zone-clock instant this character last entered/re-entered a zone (fresh world entry OR an in-process
    ///     handoff arrival) -- the legacy's <c>mTickCountFor01SecondForProtect</c> (report 05 §4 point 1:
    ///     <c>PROTECT_TICK</c> = 20 legacy ticks / 10s anti-chain-attack grace window, checked for BOTH sides of
    ///     an attack). <c>Server/ts25zone/S04_MyWork02.cpp:838,1783</c> are the field's ONLY two write sites in
    ///     the whole legacy source (registration, and the client's one-time post-load "aSort==0" action) --
    ///     it is NEVER refreshed by taking or dealing damage, so this is a one-shot spawn/arrival grace period,
    ///     not a rolling "stop hitting me" cooldown (a prior pass here refreshed it on every hit taken, which
    ///     made two players who traded even a single blow mutually unable to fight anyone for the next 10s --
    ///     see <see cref="Zone.HandleEnter" />, the sole write site now). NULL (not <see cref="TimeSpan.Zero" />)
    ///     means "never entered a zone yet" -- zero is a real, reachable zone-clock instant (a fresh zone / a
    ///     player who entered before any tick elapsed), so using it as the "never" sentinel would incorrectly
    ///     gate every player's first attack during the zone's first
    ///     <see cref="Combat.CombatResolver.ProtectDuration" /> of simulated lifetime.
    /// </summary>
    public TimeSpan? ZoneEntryAtZoneClock { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last accepted skill cast (Sort=30) -- a global, one-cast-per-
    ///     legacy-tick anti-flood gate modeled after the VERIFIED USE_INVENTORY_ITEM anti-flood pattern (report
    ///     04 §2: "1 seul 'use' par tick logique par joueur"), since reports 04/05/12 do not document a distinct
    ///     per-skill reuse-delay for generic (non-attack) skill casts -- see <c>Zone.ApplySkillCast</c>'s own
    ///     remarks and this task's StructuredOutput open issues. NULL means "never cast" -- same zero-is-a-real-
    ///     instant reasoning as <see cref="LastDamagedAtZoneClock" />.
    /// </summary>
    public TimeSpan? LastSkillCastAtZoneClock { get; set; }
}
