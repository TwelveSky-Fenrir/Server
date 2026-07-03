using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     One zone actor per hosted map (architecture reference §10.1: "un thread logique par zone, zero verrou sur
///     l'etat monde"). Every player position, the AOI grid, and the dirty-tracker marking are touched ONLY from
///     this zone's tick (<see cref="RunAsync" /> → <see cref="Tick" />) — everything else (handlers, the
///     connection host, another zone's handoff) only ever calls <see cref="Post" /> and waits for the next tick.
///     Instances are built by <see cref="ZoneRegistry" /> from <see cref="GameServerOptions.Maps" />, one
///     <see cref="RunAsync" /> task each (ADR-0012).
/// </summary>
/// <remarks>
///     The tick runs in stages (report 05 §0's legacy loop, adapted): drain inbox → simulate (whole 500 ms
///     legacy ticks via <see cref="LegacyTickAccumulator" />, decision D4) → periodic keep-alive rebroadcast
///     (avatars every 3.5 s; the monster/item 5 s slots arrive with their entity pools in Phase C — their
///     verified cadences already live in <see cref="LegacyTime" />). Implements <see cref="IZoneActor" /> so
///     <see cref="ZoneClientSession.CurrentZone" /> can carry the reference across the Network/Application
///     layer boundary.
/// </remarks>
public sealed class Zone(
    short mapId,
    GameServerOptions options,
    MovementRules movementRules,
    DirtyTracker<int> dirtyTracker,
    IReadOnlyList<ISimulationSystem> simulationSystems,
    ILogger<Zone> logger) : IZoneActor
{
    private readonly LegacyTickAccumulator _accumulator = new();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(8192) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    // ConcurrentDictionary, not a plain Dictionary: the tick is the sole WRITER (single-writer invariant intact),
    // but the write-behind flush callback and the directory-heartbeat CCU count both read this from other
    // threads -- lock-free concurrent reads are exactly what this type is for.
    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

    /// <summary>
    ///     This zone's own monotonic simulated clock: the sum of every elapsed span fed to <see cref="Tick" />,
    ///     starting at zero. Periodic cadences (avatar rebroadcast) are measured against THIS, not wall clock,
    ///     which is what makes them deterministic to test — a test drives simulated hours through
    ///     <see cref="Tick" /> in microseconds.
    /// </summary>
    private TimeSpan _clock;

    /// <summary>The legacy map this actor simulates — its key in <see cref="ZoneRegistry" />.</summary>
    public short MapId { get; } = mapId;

    public int PlayerCount => _players.Count;

    /// <summary>
    ///     Loaded once here and consumed by <see cref="HandleMove" /> via <see cref="MovementRules.IsPlausible" />
    ///     (Phase C/V1 item 1: terrain-aware movement validation — height/walkability of the TARGET position).
    ///     Monster pathing is a separate, later follow-up. Null (with a logged warning, not a startup crash) when
    ///     the <c>.WM</c> file is absent -- the legacy game-data tree is an external, multi-hundred-megabyte asset
    ///     never committed to the repo, so its absence in a given dev/CI environment must not block the zone from
    ///     ticking; <see cref="MovementRules.IsPlausible" /> degrades to speed-only validation in that case (no
    ///     regression from the pre-V1 behavior).
    /// </summary>
    public ZoneGeometry? Geometry { get; } = TryLoadGeometry(mapId, options, logger);

    /// <summary>
    ///     Cross-zone lookup this zone's OWN tick uses to resolve a handoff it initiates ITSELF rather than one
    ///     driven by an incoming client packet (currently only <see cref="ApplyDeath" />'s cross-zone revive to
    ///     a tribe capital) — set exactly once by <see cref="ZoneRegistry" /> right after constructing every
    ///     zone, before any <see cref="RunAsync" />/<see cref="Tick" /> starts (see its own remarks). Null in
    ///     unit tests that build a bare <c>Zone</c> directly (<c>ZoneTestKit</c>): the cross-zone revive branch
    ///     then degrades to a logged revive-in-place rather than throwing (see <see cref="Revive" />).
    /// </summary>
    public ZoneRegistry? Registry { get; set; }

    /// <summary>
    ///     Tribe "born spot" table (legacy <c>IsBornSpot</c>, S04_MyWork05.cpp:4776-4806) — hardcoded in the
    ///     legacy C++ itself, not sourced from any <c>.BIN</c>/<c>.IMG</c> table, so no <c>world.*</c> schema
    ///     change is warranted to carry it. Index = tribe id (0-3). Serves BOTH halves of
    ///     <see cref="ResolveDeathDestination" />: the walk-in point of a tribe's own capital, whether the
    ///     player is already there ("revive in place") or being sent home from elsewhere.
    /// </summary>
    private static readonly (short ZoneNumber, float X, float Y, float Z)[] TribeCapitals =
    [
        (1, 6f, 0f, -7f), // tribe 0
        (6, -190f, 0f, 1270f), // tribe 1
        (11, 447f, 1f, 440f), // tribe 2
        (140, 0f, 0f, -6f) // tribe 3
    ];

    /// <summary>
    ///     Enqueues a command for the next tick. Never blocks: a full inbox drops the write (architecture reference
    ///     §10.1) rather than stall whichever session thread posted it — a dropped Move is simply superseded by the client's
    ///     next one.
    /// </summary>
    public bool Post(in ZoneCommand command)
    {
        return _inbox.Writer.TryWrite(command);
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromMilliseconds(1000.0 / options.TickRateHz);
        using var timer = new PeriodicTimer(tickInterval);

        // Real elapsed time between frames is measured, not assumed to equal tickInterval: PeriodicTimer
        // coalesces missed periods, and the LegacyTickAccumulator must be paid in actual time or the 2 Hz
        // simulation would silently slow down under load.
        var lastFrame = Stopwatch.GetTimestamp();

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(lastFrame, now);
            lastFrame = now;

            Tick(elapsed);

            var tickMs = Stopwatch.GetElapsedTime(now).TotalMilliseconds;
            if (tickMs > tickInterval.TotalMilliseconds)
                logger.LogWarning("Zone {MapId} tick took {ElapsedMs:F1} ms (budget {BudgetMs:F1} ms)", MapId,
                    tickMs, tickInterval.TotalMilliseconds);
        }
    }

    /// <summary>
    ///     One network frame of this zone: drain inbox → simulate due legacy ticks → periodic rebroadcast.
    ///     Public, but with exactly two legitimate callers — <see cref="RunAsync" />'s timer loop, and tests
    ///     driving deterministic simulated time. Calling it from any other thread while <see cref="RunAsync" />
    ///     runs would break the single-writer invariant.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        _clock += elapsed;

        var t0 = Stopwatch.GetTimestamp();
        DrainInbox();
        var t1 = Stopwatch.GetTimestamp();
        Simulate(_accumulator.Advance(elapsed));
        var t2 = Stopwatch.GetTimestamp();
        ProcessPendingRevives();
        RebroadcastAvatars();
        var t3 = Stopwatch.GetTimestamp();

        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.DrainStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.SimulateStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t2, t3).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.RebroadcastStage);
    }

    private void DrainInbox()
    {
        while (_inbox.Reader.TryRead(out var command))
            try
            {
                switch (command.Kind)
                {
                    case ZoneCommandKind.Enter:
                        HandleEnter(command.CharacterId, command.EnterData!);
                        break;
                    case ZoneCommandKind.Leave:
                        HandleLeave(command.CharacterId, command.HandoffTarget, command.HandoffPosition);
                        break;
                    case ZoneCommandKind.Move:
                        var action = command.Action;
                        HandleMove(command.CharacterId, in action);
                        break;
                }
            }
            catch (Exception ex)
            {
                // One bad command must never take the whole tick loop down -- the next command, and the next
                // tick, still have to run for every OTHER player in the zone.
                logger.LogError(ex, "Zone {MapId} command {Kind} for character {CharacterId} failed", MapId,
                    command.Kind, command.CharacterId);
            }
    }

    /// <summary>
    ///     Runs every registered <see cref="ISimulationSystem" /> in declared order, once per frame that has at
    ///     least one whole 500 ms legacy tick due (decision D4). Empty list today: the monster/buff/regen/spawn
    ///     systems arrive in Phase C — this is their wiring point, kept live (and metered) so adding a system is
    ///     purely additive.
    /// </summary>
    private void Simulate(int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        foreach (var system in simulationSystems)
            try
            {
                system.Simulate(this, legacyTicksElapsed);
            }
            catch (Exception ex)
            {
                // Same containment posture as DrainInbox: one faulty system must not starve the others, nor
                // the rebroadcast stage after it.
                logger.LogError(ex, "Zone {MapId} simulation system {System} failed", MapId,
                    system.GetType().Name);
            }
    }

    /// <summary>
    ///     Keep-alive rebroadcast (report 05 §0 item 6): the legacy loop re-emits every avatar's current state
    ///     to its surroundings every 3.5 s (<c>tLogicAvatarTick</c>) even when idle, so late-arriving or
    ///     packet-lossy neighbors converge. Same wire packet as a move (<see cref="ZcAvatarActionRecv" />),
    ///     serialize-once per avatar via <see cref="BroadcastAvatarAction" />. Monsters and ground items get
    ///     their own 5 s pass here in Phase C (<see cref="LegacyTime.MonsterRebroadcastInterval" />).
    /// </summary>
    private void RebroadcastAvatars()
    {
        // Direct enumeration (no Values snapshot): ConcurrentDictionary's enumerator is lock-free, and the
        // tick thread is the only mutator anyway.
        foreach (var (characterId, state) in _players)
        {
            if (_clock - state.LastAvatarRebroadcastAt < LegacyTime.AvatarRebroadcastInterval)
                continue;

            state.LastAvatarRebroadcastAt = _clock;

            var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
        }
    }

    private void HandleEnter(int characterId, PlayerEnterData data)
    {
        var state = new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = data.Session,
            Name = data.Name,
            Tribe = data.Tribe,
            Gender = data.Gender,
            HeadType = data.HeadType,
            FaceType = data.FaceType,
            Level = data.Level,
            MapId = data.MapId,
            PosX = data.PosX,
            PosY = data.PosY,
            PosZ = data.PosZ,
            Heading = data.Heading,
            Life = data.Life,
            MaxLife = data.MaxLife,
            Mana = data.Mana,
            MaxMana = data.MaxMana,
            FlushSequence = data.FlushSequence,
            LastMoveUtc = DateTime.UtcNow,
            LastAvatarRebroadcastAt = _clock
        };

        var cell = _grid.CellOf(state.PosX, state.PosZ);
        state.CurrentCell = cell;

        if (!_players.TryAdd(characterId, state))
        {
            logger.LogWarning(
                "Character {CharacterId} entered zone {MapId} while already tracked -- ignoring duplicate Enter",
                characterId, MapId);
            return;
        }

        _grid.Add(characterId, cell);

        // Position is marked dirty on entry so a HANDOFF's map change reaches SQL even if the player never
        // moves again (ZoneTransfer bumps FlushSequence for exactly this reason). On a fresh world entry the
        // sequence still equals the DB baseline, so usp_Character_PersistBatch's strictly-greater guard makes
        // the flushed row a deliberate no-op -- one wasted row per login, zero special-casing.
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Mutual visibility: existing neighbors learn about the new arrival, and the new arrival learns about
        // them. The self-spawn ZC_AVATAR_ACTION_RECV (showing the new player their OWN avatar) is sent directly
        // by the registration handler before this command is even posted -- this is only the cross-player half.
        var others = _grid.Neighbors(cell).Where(id => id != characterId).ToArray();

        // The new arrival learns about each already-present neighbor via a direct send to ITS OWN session
        // (mirrors the self-spawn packet's own direct-send treatment); existing neighbors learn about the new
        // arrival via the shared serialize-once broadcast right below. Swapping these two arguments would send
        // the new arrival's own data to itself twice and leave it blind to everyone already there.
        foreach (var otherId in others)
            if (_players.TryGetValue(otherId, out var other))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(others, state);
    }

    private void HandleLeave(int characterId, Zone? handoffTarget, (float X, float Y, float Z)? handoffPosition = null)
    {
        if (!_players.TryRemove(characterId, out var state))
            return;

        _grid.Remove(characterId, state.CurrentCell);

        if (handoffTarget is null)
            // Plain leave (disconnect). No wire mechanism resolved in Phase 0 for "entity removed" (§5.9: no
            // despawn/logout opcode exists in the M1 client protocol) -- nearby clients simply stop receiving
            // updates for this entity. A documented M1 gap, not an oversight: reproducing it would require
            // inventing an opcode the real client never sends or expects.
            return;

        // In-process map transfer (ADR-0012): the live state is SNAPSHOTTED into the Enter command and travels
        // inside it -- this zone has already forgotten the player (TryRemove above), the target zone only
        // learns of them when ITS tick drains the command, so the character never exists in two zones at once
        // and no state is ever shared across ticks. handoffPosition (set by a portal/NPC-transfer handler or by
        // ApplyDeath's cross-zone revive) overrides where the snapshot lands -- see ZoneCommand.HandoffPosition.
        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, handoffPosition);

        if (!handoffTarget.Post(ZoneCommand.Enter(characterId, enterData)))
        {
            // Same severity rationale as the registration handler's dropped Enter: the player is now in NO
            // zone, permanently invisible to AOI/broadcast/persistence, while their client still believes it
            // is in the world. Fail loudly and drop the connection rather than leave a phantom.
            logger.LogError(
                "Zone {TargetMapId} inbox full: dropped handoff Enter for character {CharacterId} from zone {MapId} -- aborting session",
                handoffTarget.MapId, characterId, MapId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        // Re-point the session at its new zone from the source tick (a plain reference write: atomic, and a
        // stale read by a racing movement handler is benign -- see ZoneClientSession.CurrentZone's remarks).
        if (state.Session is ZoneClientSession zoneSession)
            zoneSession.CurrentZone = handoffTarget;
    }

    /// <summary>
    ///     Kills <paramref name="characterId" /> in this zone: Life → 0, <see cref="PlayerRuntimeState.IsDead" />
    ///     set, and an automatic revive scheduled <see cref="LegacyTime.DeathReviveDelay" /> later (report 12
    ///     §4.2). PUBLIC and characterId-addressed on purpose: the Phase C/V3 combat handler (killing-blow
    ///     resolution) is the intended caller, and it must never need a <see cref="PlayerRuntimeState" />
    ///     reference itself — only this zone's own tick may construct/mutate one (single-writer invariant,
    ///     architecture reference §10.1). A no-op (logged) if the character is not tracked here — defensive
    ///     against a race between a killing blow's command and a disconnect/handoff <c>Leave</c> for the same
    ///     tick; also a no-op if the character is ALREADY dead, so a duplicate killing blow (e.g. an AoE that
    ///     hits an already-dying target twice in the same tick) never re-arms the revive timer.
    /// </summary>
    /// <remarks>
    ///     XP penalty on death (report 05 §4/§6, <c>ProcessAttack02/04</c>) is NOT applied here — Fenrir has no
    ///     XP/leveling system yet (Phase C/V3 territory); tracked as an explicit open issue rather than silently
    ///     skipped (see this task's StructuredOutput). Movement/interaction gating on <c>IsDead</c> (legacy
    ///     blocks potions and most actions while <c>aAction.aSort</c> is 11/stun or 12/death) is likewise left
    ///     for whichever Phase C/V3 handler needs it — this method only ever sets the flag.
    /// </remarks>
    public void ApplyDeath(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
        {
            logger.LogWarning(
                "ApplyDeath({CharacterId}) on zone {MapId}: character not tracked here -- ignoring (already disconnected or mid-handoff)",
                characterId, MapId);
            return;
        }

        if (state.IsDead)
            return;

        state.Life = 0;
        state.IsDead = true;

        var destination = ResolveDeathDestination(state.Tribe);
        state.ReviveZoneNumber = destination.ZoneNumber;
        state.ReviveX = destination.X;
        state.ReviveY = destination.Y;
        state.ReviveZ = destination.Z;
        state.ReviveAtZoneClock = _clock + LegacyTime.DeathReviveDelay;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);

        // Death pose (report 12 §4.2: aAction.aSort = 12) so nearby clients see the character fall immediately
        // -- same broadcast machinery as any other avatar-state change. Self is excluded, same posture as
        // HandleMove: the future combat handler (Phase C/V3) is responsible for telling the dying player's OWN
        // client about the killing blow via combat-result packets, which do not exist yet.
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, deathAction);
    }

    /// <summary>
    ///     Legacy-documented respawn target for a death in THIS zone (report 12 §4.2, <c>ZONEMOVEINFO::ReturnNextZoneAfterDeath</c>,
    ///     Header/S19_MyZoneMoveInfo.cpp:1280): dying already inside one's own tribe capital revives IN PLACE;
    ///     dying anywhere else sends the player home to that capital. Both branches resolve to the same
    ///     <see cref="TribeCapitals" /> row -- "in place" is simply the case where that row's zone already
    ///     equals <see cref="MapId" />.
    /// </summary>
    /// <remarks>
    ///     DOCUMENTED SIMPLIFICATION (D8 iso-behavior policy — a real gap, not a silent guess): the legacy rule
    ///     ALSO keeps a player in place when the current zone belongs to their tribe's ALLIANCE or is NEUTRAL
    ///     (<c>ReturnZoneTribeInfo1 == -1</c>), and falls back to zone 38 in some further unspecified cases
    ///     ("selon les cas", report 12). Neither branch is reproducible today: <c>world.Zones</c> carries no
    ///     tribe-ownership column (report 05 §12's <c>ZONEMAININFO</c> mapping was never migrated, see
    ///     Database/30_tables/world/Zones.sql) and Fenrir has no alliance system yet (Phase C/V7). This method
    ///     therefore only ever returns "same zone" (own capital) or "own capital" — never a third-party zone or
    ///     zone 38. A death in a neutral field zone will always send the player home instead of reviving them in
    ///     place, a conservative divergence flagged as an open issue rather than guessed at.
    /// </remarks>
    private (short ZoneNumber, float X, float Y, float Z) ResolveDeathDestination(byte tribe)
    {
        if (tribe >= TribeCapitals.Length)
        {
            logger.LogWarning(
                "ResolveDeathDestination: tribe {Tribe} has no known capital (expected 0-3) -- defaulting to tribe 0's",
                tribe);
            return TribeCapitals[0];
        }

        return TribeCapitals[tribe];
    }

    /// <summary>
    ///     Sweeps every dead player whose scheduled revive (<see cref="ApplyDeath" />) is due, in this frame's
    ///     tick -- same enumeration posture as <see cref="RebroadcastAvatars" />: this tick thread is the sole
    ///     mutator, so a lock-free direct enumeration of <see cref="_players" /> is safe.
    /// </summary>
    private void ProcessPendingRevives()
    {
        foreach (var (characterId, state) in _players)
        {
            if (!state.IsDead || _clock < state.ReviveAtZoneClock)
                continue;

            Revive(characterId, state);
        }
    }

    /// <summary>
    ///     Executes a due revive resolved by <see cref="ApplyDeath" />: HP forced to 1 regardless of MaxLife
    ///     (report 12 §4.2, matching the legacy <c>REGISTER_AVATAR_SEND</c> flow's own force-to-1), then either
    ///     a same-zone reposition or a cross-zone handoff reusing the EXACT same in-process mechanism as a
    ///     client-driven portal transfer (<see cref="HandleLeave" />) — just triggered by this zone's own tick
    ///     instead of a client packet.
    /// </summary>
    private void Revive(int characterId, PlayerRuntimeState state)
    {
        state.IsDead = false;
        state.Life = 1;

        if (state.ReviveZoneNumber == MapId)
        {
            state.PosX = state.ReviveX;
            state.PosY = state.ReviveY;
            state.PosZ = state.ReviveZ;

            var newCell = _grid.CellOf(state.PosX, state.PosZ);
            _grid.Move(characterId, state.CurrentCell, newCell);
            state.CurrentCell = newCell;

            dirtyTracker.MarkDirty(characterId, DirtyFlags.Position | DirtyFlags.Vitals);

            SendAvatarAction(state.Session, state);
            var neighbors = _grid.Neighbors(newCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
            return;
        }

        if (Registry is null || !Registry.TryGet(state.ReviveZoneNumber, out var targetZone))
        {
            // No registry wired (a bare Zone in a unit test) or the capital isn't hosted by this shard
            // (ADR-0012 -- same "out of scope, single shard today" posture as CzDemandZoneServerInfo2SendHandler):
            // degrade to reviving in place rather than stranding the player mid-death forever.
            logger.LogWarning(
                "Character {CharacterId} died in zone {MapId} but its revive target zone {TargetZone} is not resolvable -- reviving in place instead",
                characterId, MapId, state.ReviveZoneNumber);

            dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);
            SendAvatarAction(state.Session, state);
            var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
            BroadcastAvatarAction(neighbors, state);
            return;
        }

        // Cross-zone revive: same-thread call, not a re-posted command -- we ARE this zone's tick right now,
        // so calling HandleLeave directly preserves the single-writer invariant exactly as well as draining it
        // from the inbox would, without paying an extra tick's latency.
        HandleLeave(characterId, targetZone, (state.ReviveX, state.ReviveY, state.ReviveZ));
    }

    private void HandleMove(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        var now = DateTime.UtcNow;

        if (!movementRules.IsPlausible(state, in action, now, Geometry))
        {
            // Reject: reply with the player's own last-known-good state so the client corrects itself, per
            // architecture reference §6.5's ForcePositionSync idea -- adapted to the legacy wire by reusing the
            // same ZC_AVATAR_ACTION_RECV struct the client already understands (no ForcePositionSync packet
            // exists in the M1 protocol; this IS that mechanism on this wire).
            SendAvatarAction(state.Session, state);
            return;
        }

        state.PosX = action.Location[0];
        state.PosY = action.Location[1];
        state.PosZ = action.Location[2];
        state.Heading = action.Front;
        state.LastMoveUtc = now;
        state.FlushSequence++;

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Self is excluded: the legacy client applies its own movement locally (client-side prediction) and
        // does not need its own action echoed back to it.
        var neighbors = _grid.Neighbors(newCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, action);
    }

    private static void SendAvatarAction(IPacketSession session, PlayerRuntimeState state)
    {
        session.Send(BuildAvatarActionRecv(state));
    }

    /// <summary>
    ///     Serialize-once broadcast (architecture reference §10.4, "Decision D-07"): the frame is written to a rented
    ///     buffer ONE time and copied into each recipient's own pipe, instead of re-serializing the packet per recipient.
    /// </summary>
    private void BroadcastAvatarAction(IReadOnlyList<int> recipientCharacterIds, PlayerRuntimeState state,
        ActionInfo? action = null)
    {
        if (recipientCharacterIds.Count == 0)
            return;

        var packet = action is null ? BuildAvatarActionRecv(state) : BuildAvatarActionRecv(state, action.Value);
        var total = FrameWriter.FrameSizeOf<ZcAvatarActionRecv>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipientCharacterIds)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    // Same containment posture as DrainInbox/Simulate: a recipient whose transport is already
                    // gone (e.g. a disconnect whose Leave command hasn't drained yet) must not abort the
                    // broadcast for every OTHER recipient, nor bubble out of RunAsync's tick loop and kill this
                    // zone's whole tick task (ZoneTickHost awaits Task.WhenAll over every zone).
                    logger.LogError(ex, "Zone {MapId} broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static ZcAvatarActionRecv BuildAvatarActionRecv(PlayerRuntimeState state)
    {
        return BuildAvatarActionRecv(state, new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        });
    }

    /// <summary>
    ///     Internal (not private): reused by <c>CzDemandZoneServerInfo2SendHandler</c> to build the self-spawn
    ///     packet for a zone-transfer's fresh world-state push, with an explicit <paramref name="action" />
    ///     carrying the just-resolved ARRIVAL position rather than <paramref name="state" />'s own (still the
    ///     source zone's, single-writer invariant preserved -- see that handler's remarks).
    /// </summary>
    internal static ZcAvatarActionRecv BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new ZcAvatarActionRecv
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = state.Name,
                Tribe = state.Tribe,
                PreviousTribe = 0,
                Gender = state.Gender,
                HeadType = state.HeadType,
                FaceType = state.FaceType,
                Level1 = state.Level,
                Level2 = 0,
                EquipForView = new int[26],
                AnimalNumber = 0,
                Title = 0,
                Halo = 0,
                RebirthNum = 0,
                BattleTeam = 0,
                Action = action,
                MaxLifeValue = state.MaxLife,
                LifeValue = state.Life,
                MaxManaValue = state.MaxMana,
                ManaValue = state.Mana,
                EffectValueForView = new int[35],
                PartyName = "",
                DuelState = new int[3],
                PShopState = 0,
                PShopName = "",
                CostumeNumber = 0,
                BufEffectTimeState = 0,
                BufSort = 0,
                AutoState = 0,
                FishingState = 0,
                FishingStep = 0,
                FishingPoint = new float[3],
                RankPoint = 0,
                TargetState = 0,
                AnimalAbsorbState = 0,
                PetValid = 0,
                Unk1 = 0,
                PetLocation = new float[3],
                PetFrame = 0,
                Unk624 = 0,
                Unk625 = 0,
                UniqueSkillNumber = 0,
                UniqueSkillBuffTime = 0,
                CostumeState = 0,
                StellarCoreNumber = 0
            },
            CheckChangeActionState = 0
        };
    }

    /// <summary>
    ///     Resolves <c>{GameDataDirectory}/WORLD/Z{mapId:D3}.WM</c> against the process's current working
    ///     directory -- matching the legacy <c>ServerInfo.ini</c>'s own <c>DataDir=./DATA/</c> convention (always
    ///     relative to wherever the process was launched from), which is also how Aspire launches project
    ///     resources in dev (working directory = the project's own source folder, where the <c>GameData</c>
    ///     junction lives).
    /// </summary>
    private static ZoneGeometry? TryLoadGeometry(short mapId, GameServerOptions gameServerOptions,
        ILogger<Zone> zoneLogger)
    {
        var wmPath = Path.Combine(Directory.GetCurrentDirectory(), gameServerOptions.GameDataDirectory, "WORLD",
            $"Z{mapId:D3}.WM");

        if (!File.Exists(wmPath))
        {
            zoneLogger.LogWarning(
                "No world geometry found at {Path} for MapId {MapId} -- movement validation continues without terrain awareness",
                wmPath, mapId);
            return null;
        }

        try
        {
            return ZoneGeometryReader.Load(wmPath);
        }
        catch (Exception ex)
        {
            zoneLogger.LogError(ex, "Failed to load world geometry from {Path}", wmPath);
            return null;
        }
    }
}
