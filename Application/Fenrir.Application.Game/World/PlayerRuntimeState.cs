using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Abstractions;

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
}
