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
    ///     <c>TimeSpan</c> clock, not wall time) at which <see cref="Zone.ApplyDeath" />'s scheduled revive
    ///     fires. Meaningless while <see cref="IsDead" /> is false.
    /// </summary>
    public TimeSpan ReviveAtZoneClock { get; set; }

    /// <summary>
    ///     Resolved once by <see cref="Zone.ApplyDeath" /> (legacy <c>ZONEMOVEINFO::ReturnNextZoneAfterDeath</c>,
    ///     report 12 §4.2 / report 05 §7): the zone the automatic revive lands in — this player's OWN
    ///     <see cref="MapId" /> at the time of death for "revive in place", or their tribe's capital zone
    ///     otherwise. Meaningless while <see cref="IsDead" /> is false.
    /// </summary>
    public short ReviveZoneNumber { get; set; }

    /// <summary>Arrival coordinates for the pending revive — see <see cref="ReviveZoneNumber" />.</summary>
    public float ReviveX { get; set; }

    public float ReviveY { get; set; }
    public float ReviveZ { get; set; }
}
