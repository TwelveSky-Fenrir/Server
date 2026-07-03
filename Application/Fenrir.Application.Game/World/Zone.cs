using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.Monsters;
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
    ILogger<Zone> logger,
    WorldDataCache worldData,
    IRandomSource? randomSource = null) : IZoneActor
{
    private readonly LegacyTickAccumulator _accumulator = new();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(8192) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Third inbox, alongside <see cref="_inbox" />/<see cref="_inventoryInbox" />: raw, UNVALIDATED
    ///     CZ_PROCESS_ATTACK_SEND requests (<see cref="CombatCommand" />'s own remarks explain why this is
    ///     zero-SQL, tick-thread-resolved combat rather than a pre-decided mirror like the inventory channel).
    /// </summary>
    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(4096) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Combat/skill RNG -- <see cref="SystemRandomSource" /> in production, injectable for deterministic tests.</summary>
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

    /// <summary>
    ///     Second, SEPARATE inbox for already-validated-and-SQL-durable inventory results
    ///     (<see cref="InventoryZoneCommand" />, posted by <c>GenericActionHandler</c>). Deliberately not
    ///     folded into <see cref="_inbox" />/<see cref="ZoneCommand" />'s own union: this task's perimeter is
    ///     additive only (Application/Fenrir.Application.Game/Inventory/ + Handlers/) and must never require
    ///     editing <see cref="DrainInbox" />'s existing switch -- see <see cref="DrainInventoryCommands" />'s
    ///     own remarks for the full rationale. Same bounded/drop-on-full posture as <see cref="_inbox" />: by
    ///     the time a command reaches here its SQL write already committed, so a dropped command only leaves
    ///     this zone's in-memory mirror stale (self-heals on the player's next world entry), never a lost item.
    /// </summary>
    private readonly Channel<InventoryZoneCommand> _inventoryInbox = Channel.CreateBounded<InventoryZoneCommand>(
        new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    // ConcurrentDictionary, not a plain Dictionary: the tick is the sole WRITER (single-writer invariant intact),
    // but the write-behind flush callback and the directory-heartbeat CCU count both read this from other
    // threads -- lock-free concurrent reads are exactly what this type is for.
    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

    // V4 (Monsters & Loot): same ConcurrentDictionary posture as _players -- the tick (MonsterSpawnScheduler/
    // MonsterAiSystem) is the sole writer for spawn/AI mutation, but TryDamageMonster is a deliberate, narrow
    // exception (mirrors ApplyDeath's own established precedent) that lets a combat packet handler thread
    // apply damage directly via an atomic Interlocked path on MonsterEntity itself -- see that method's remarks.
    private readonly ConcurrentDictionary<int, MonsterEntity> _monsters = new();

    // Populated/expired ONLY by this zone's own tick (single-writer); CLAIMED (removed) via an atomic
    // compare-and-remove callable from ANY thread -- see TryClaimGroundItem's remarks for why pickup is the
    // one narrow exception, mirroring the same reasoning already accepted for TryDamageMonster/ApplyDeath.
    private readonly ConcurrentDictionary<int, GroundItemEntity> _groundItems = new();

    /// <summary>Tick-owned only (rebroadcast/expiry both run on the tick thread) -- see <see cref="_groundItems" />'s remarks.</summary>
    private readonly Dictionary<int, TimeSpan> _groundItemLastRebroadcast = new();

    /// <summary>Enqueued by <see cref="TryDamageMonster" /> (any thread) on a killing blow, drained by <see cref="Monsters.MonsterSpawnScheduler" /> on this zone's own next tick (loot/XP/respawn -- single-writer preserved).</summary>
    private readonly ConcurrentQueue<DeadMonsterEvent> _deadMonsters = new();

    /// <summary>Enqueued by <see cref="TryClaimGroundItem" /> (any thread) so the despawn BROADCAST (needs <see cref="_grid" />, tick-thread-only) happens from the tick, never from the claiming handler's own thread.</summary>
    private readonly ConcurrentQueue<GroundItemEntity> _claimedGroundItemDespawns = new();

    /// <summary>
    ///     Server-initiated monster-kill money grants (<see cref="Monsters.MonsterSpawnScheduler" />'s own
    ///     loot pipeline) -- queued rather than awaited inline because <see cref="Tick" /> is fully synchronous
    ///     and must never block on SQL I/O; drained by a dedicated background flush host
    ///     (<c>Fenrir.GameServer.MonsterLootFlushHost</c>) from any thread (<see cref="ConcurrentQueue{T}" />'s
    ///     own thread-safety). Unlike a client-requested pickup (D7 regime, awaited synchronously in
    ///     <c>GenericActionHandler</c>), a kill reward has no client ack to gate on durability.
    /// </summary>
    private readonly ConcurrentQueue<(int CharacterId, long Amount)> _pendingMoneyGrants = new();

    /// <summary>
    ///     Released once per queued grant so <c>MonsterLootFlushHost</c> can flush as soon as a grant arrives
    ///     (racing against its own periodic timer) instead of waiting up to a full flush interval -- shrinks the
    ///     in-memory-only loss window (review finding: money-grant durability was flagged as D7-regime-adjacent
    ///     even though a kill has no client ack to gate on, per <see cref="_pendingMoneyGrants" />'s own remarks)
    ///     down to roughly one SQL round trip instead of a fixed worst case.
    /// </summary>
    private readonly SemaphoreSlim _moneyGrantSignal = new(0, int.MaxValue);

    private int _monsterUniqueNumberSeed;
    private int _groundItemServerIndexSeed;
    private int _groundItemUniqueNumberSeed;

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

    /// <summary>Live monster count in this zone -- test/inspection surface, same posture as <see cref="PlayerCount" />.</summary>
    public int MonsterCount => _monsters.Count;

    /// <summary>Live ground-item count in this zone -- test/inspection surface, same posture as <see cref="PlayerCount" />.</summary>
    public int GroundItemCount => _groundItems.Count;

    /// <summary>
    ///     Every currently-tracked player in this zone -- read-only enumeration for <see cref="ISimulationSystem" />s
    ///     that run on this zone's own tick thread (buffs, meditation regen). Never a mutable view: a system may
    ///     mutate the yielded <see cref="PlayerRuntimeState" /> instances directly (it runs on the single-writer
    ///     tick thread, same posture as <see cref="DrainInbox" />), but must never add/remove entries here.
    /// </summary>
    public IEnumerable<PlayerRuntimeState> Players => _players.Values;

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
    ///     Enqueues a command for the next tick. Never blocks: a full inbox drops the write (architecture reference
    ///     §10.1) rather than stall whichever session thread posted it — a dropped Move is simply superseded by the client's
    ///     next one.
    /// </summary>
    public bool Post(in ZoneCommand command)
    {
        return _inbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Enqueues an already-validated, already-SQL-durable inventory result for this zone's own tick to
    ///     mirror into <see cref="PlayerRuntimeState.Inventory" />/<see cref="PlayerRuntimeState.Stats" /> --
    ///     see <see cref="_inventoryInbox" />'s remarks for why this is a separate channel from
    ///     <see cref="Post" />/<see cref="ZoneCommand" />.
    /// </summary>
    public bool PostInventoryCommand(in InventoryZoneCommand command)
    {
        return _inventoryInbox.Writer.TryWrite(command);
    }

    /// <summary>
    ///     Enqueues a raw, unvalidated CZ_PROCESS_ATTACK_SEND request (<c>AttackHandler</c>) for this zone's own
    ///     tick to resolve -- see <see cref="CombatCommand" />'s remarks for why combat is resolved entirely on
    ///     the tick thread rather than pre-decided by the posting handler.
    /// </summary>
    public bool PostCombatCommand(in CombatCommand command)
    {
        return _combatInbox.Writer.TryWrite(command);
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

    // ---- V4 (Monsters & Loot) public/internal surface ----

    public bool TryGetMonster(int serverIndex, out MonsterEntity? monster)
    {
        return _monsters.TryGetValue(serverIndex, out monster);
    }

    /// <summary>Every monster currently alive in this zone -- read by <see cref="Monsters.MonsterAiSystem" /> (tick-thread-only enumeration, same lock-free posture as <see cref="Players" />).</summary>
    internal IEnumerable<MonsterEntity> MonstersSnapshot => _monsters.Values;

    /// <summary>
    ///     Player character ids in this zone's AOI grid neighborhood of (x, z) -- reused for BOTH monster aggro
    ///     detection (<see cref="Monsters.MonsterAiSystem" />) and monster/ground-item replication audience
    ///     (report 05 §3's "cellules spatiales ±1/±2", simplified to this grid's own single-radius neighbor
    ///     set). Tick-thread-only: <see cref="_grid" /> itself is not thread-safe (see its own remarks).
    /// </summary>
    internal IEnumerable<int> NeighborsOfPosition(float x, float z)
    {
        return _grid.Neighbors(_grid.CellOf(x, z));
    }

    internal uint NextMonsterUniqueNumber()
    {
        return unchecked((uint)Interlocked.Increment(ref _monsterUniqueNumberSeed));
    }

    /// <summary>Adds a freshly-spawned monster to this zone's live pool and broadcasts its creation. Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />).</summary>
    internal void SpawnMonster(MonsterEntity monster)
    {
        monster.LastRebroadcastAt = _clock;
        _monsters[monster.ServerIndex] = monster;
        BroadcastMonsterAction(monster, 1); // report 05 §1: "action=1" on B_MONSTER_ACTION_RECV at creation
    }

    /// <summary>
    ///     Applies damage to a monster. Safe from ANY thread (see <see cref="MonsterEntity.TakeDamage" />'s
    ///     remarks) -- the intended callers are <see cref="ApplyPvmAttack" /> (this zone's own tick, mCase 3)
    ///     and, eventually, any other combat surface. On the killing blow, atomically removes the monster from
    ///     this zone's live pool and queues a <see cref="DeadMonsterEvent" /> for THIS zone's own next tick to
    ///     process (loot/XP/respawn) -- never processed inline here, regardless of caller thread.
    /// </summary>
    public bool TryDamageMonster(int serverIndex, int amount, int? attackerCharacterId, out bool died,
        out int remainingLife)
    {
        if (!_monsters.TryGetValue(serverIndex, out var monster))
        {
            died = false;
            remainingLife = 0;
            return false;
        }

        died = monster.TakeDamage(amount, out remainingLife);
        if (died)
        {
            _monsters.TryRemove(serverIndex, out _);
            _deadMonsters.Enqueue(new DeadMonsterEvent(monster, attackerCharacterId));
        }

        return true;
    }

    internal bool TryDequeueDeadMonster(out DeadMonsterEvent? deadMonster)
    {
        return _deadMonsters.TryDequeue(out deadMonster);
    }

    /// <summary>Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />'s dead-monster drain) -- sets the transient <see cref="MonsterAiState.Dead" /> bookkeeping value and broadcasts the final (LifeValue == 0) replication frame.</summary>
    internal void BroadcastMonsterDeath(MonsterEntity monster)
    {
        monster.AiState = MonsterAiState.Dead;
        BroadcastMonsterAction(monster, 0);
    }

    /// <summary>
    ///     AI-initiated MvP attack (report 05 §3/§4: the monster's OWN AI calls <c>ProcessAttack04</c> directly,
    ///     never via a client packet in practice, <c>S07_MyGame05.cpp:3961</c>) -- the intended caller is
    ///     <see cref="Monsters.MonsterAiSystem" />'s attack-windup state, running on this SAME zone's tick
    ///     thread (single-writer invariant preserved: this method mutates <paramref name="targetCharacterId" />'s
    ///     live <see cref="PlayerRuntimeState" /> directly).
    /// </summary>
    internal void ResolveMonsterAttack(MonsterEntity monster, int targetCharacterId)
    {
        if (!_players.TryGetValue(targetCharacterId, out var target) || target is null)
            return;

        var defenderSnapshot = ToCombatantSnapshot(target);
        var outcome = MonsterCombatResolver.ResolveMvpAttack(monster, defenderSnapshot, _clock, _random);
        if (outcome.Rejected)
            return;

        var response = new AttackResponse
        {
            AttackInfo = new AttackForProtocol
            {
                Case = 4,
                ServerIndex1 = monster.ServerIndex,
                UniqueNumber1 = monster.UniqueNumber,
                ServerIndex2 = target.CharacterId,
                UniqueNumber2 = target.UniqueNumber,
                SenderLocation = [monster.PosX, monster.PosY, monster.PosZ],
                AttackActionValue1 = 1,
                AttackActionValue2 = 0,
                AttackActionValue3 = 0,
                AttackActionValue4 = 0,
                AttackResultValue = outcome.Hit ? 1 : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { target.CharacterId };
        foreach (var id in _grid.Neighbors(target.CurrentCell)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        target.Life -= outcome.DamageApplied;
        dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Vitals);

        if (target.Life <= 0)
            ApplyDeath(target.CharacterId, DeathCause.MonsterKill);
    }

    /// <summary>Queued for <c>Fenrir.GameServer.MonsterLootFlushHost</c> to persist -- see <see cref="_pendingMoneyGrants" />'s remarks.</summary>
    internal void QueueMoneyGrant(int characterId, long amount)
    {
        _pendingMoneyGrants.Enqueue((characterId, amount));
        _moneyGrantSignal.Release();
    }

    /// <summary>
    ///     Resolves as soon as a grant is queued (or immediately, if one is already pending un-awaited) -- lets
    ///     <c>MonsterLootFlushHost</c> race this against its own periodic timer via <c>Task.WhenAny</c> rather
    ///     than only ever waking up on the timer's fixed cadence.
    /// </summary>
    public Task WaitForMoneyGrantAsync(CancellationToken ct)
    {
        return _moneyGrantSignal.WaitAsync(ct);
    }

    /// <summary>Drains every pending monster-kill money grant queued since the last drain -- callable from ANY thread (<see cref="ConcurrentQueue{T}" />'s own thread-safety); the ONLY intended caller is the background flush host.</summary>
    public IReadOnlyList<(int CharacterId, long Amount)> DrainPendingMoneyGrants()
    {
        if (_pendingMoneyGrants.IsEmpty)
            return [];

        List<(int CharacterId, long Amount)>? grants = null;
        while (_pendingMoneyGrants.TryDequeue(out var grant))
            (grants ??= []).Add(grant);

        return (IReadOnlyList<(int CharacterId, long Amount)>?)grants ?? [];
    }

    /// <summary>Spawns one dropped item on the ground (report 05 §5's generic drop pipeline) and broadcasts its creation. Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />).</summary>
    internal void SpawnGroundItem(int itemId, int quantity, float posX, float posY, float posZ, string master,
        string partyName, int dropSort)
    {
        var index = Interlocked.Increment(ref _groundItemServerIndexSeed);
        var uniqueNumber = unchecked((uint)Interlocked.Increment(ref _groundItemUniqueNumberSeed));

        var entity = new GroundItemEntity(index, uniqueNumber, itemId, quantity, Value: 0, SerialNumber: 0, posX,
            posY, posZ, TruncateName(master), TruncateName(partyName), dropSort, _clock, SocketGem1: 0,
            SocketGem2: 0, SocketGem3: 0);

        _groundItems[index] = entity;
        _groundItemLastRebroadcast[index] = _clock;
        BroadcastGroundItemAction(entity, 1); // report 05 §5 creation, by analogy with the verified monster "action=1"
    }

    private static string TruncateName(string name)
    {
        return name.Length <= 13 ? name : name[..13]; // MAX_AVATAR_NAME_LENGTH
    }

    /// <summary>
    ///     Atomically claims (removes) a ground item for pickup -- callable from ANY thread (the intended
    ///     caller is <c>GenericActionHandler</c>'s tSort 201 branch, a session-thread async handler). Ownership
    ///     window and distance are checked against IMMUTABLE snapshot fields (never mutated after drop, see
    ///     <see cref="GroundItemEntity" />'s own remarks), so only the actual removal needs to be atomic:
    ///     <see cref="ConcurrentDictionary{TKey,TValue}" />'s own <c>ICollection.Remove(KeyValuePair)</c> is a
    ///     compare-and-remove keyed on the CURRENT value still matching the snapshot just read -- exactly the
    ///     "first claimant wins" guarantee needed when two players target the same item concurrently, with zero
    ///     custom locking and no risk of ever duplicating the item. The despawn BROADCAST is deliberately NOT
    ///     done here (it needs <see cref="_grid" />, tick-thread-only) -- see <see cref="_claimedGroundItemDespawns" />.
    /// </summary>
    public GroundItemClaimOutcome TryClaimGroundItem(int serverIndex, uint expectedUniqueNumber, string claimantName,
        string? claimantPartyName, float claimantX, float claimantY, float claimantZ, out GroundItemEntity? item)
    {
        if (!_groundItems.TryGetValue(serverIndex, out var snapshot) ||
            snapshot.UniqueNumber != expectedUniqueNumber)
        {
            item = null;
            return GroundItemClaimOutcome.NotFound;
        }

        if (!snapshot.IsClaimableBy(claimantName, claimantPartyName, _clock))
        {
            item = null;
            return GroundItemClaimOutcome.NotOwned;
        }

        // ITEM_OBJECT::CheckPossibleGetItem (S07_MyGame06.cpp:63): GetLengthXYZ -- full 3D distance, NOT
        // XZ-only (a prior pass here discarded claimantY entirely, citing an unrelated function's own XZ-only
        // posture; this specific check really is 3D in the verified source).
        var dx = snapshot.PosX - claimantX;
        var dy = snapshot.PosY - claimantY;
        var dz = snapshot.PosZ - claimantZ;
        if (dx * dx + dy * dy + dz * dz >
            GroundItemPickupPolicy.MaxPickupDistance * GroundItemPickupPolicy.MaxPickupDistance)
        {
            item = null;
            return GroundItemClaimOutcome.TooFar;
        }

        if (!((ICollection<KeyValuePair<int, GroundItemEntity>>)_groundItems).Remove(
                new KeyValuePair<int, GroundItemEntity>(serverIndex, snapshot)))
        {
            item = null;
            return GroundItemClaimOutcome.NotFound; // lost the race to a concurrent claimant
        }

        _claimedGroundItemDespawns.Enqueue(snapshot);
        item = snapshot;
        return GroundItemClaimOutcome.Success;
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
        // Separate inbox/method on purpose (see _inventoryInbox's remarks): this task's perimeter must stay
        // additive-only and never edit DrainInbox's own switch. Folded into the same "Drain" timing bucket
        // below since it is, in spirit, just another inbox drain.
        DrainInventoryCommands();
        // Same additive posture as DrainInventoryCommands -- see CombatCommand's own remarks.
        DrainCombatCommands();
        var t1 = Stopwatch.GetTimestamp();
        Simulate(_accumulator.Advance(elapsed));
        var t2 = Stopwatch.GetTimestamp();
        ProcessPendingRevives();
        RebroadcastAvatars();
        // V4 (Monsters & Loot): claimed-item despawn broadcasts first (a claim may have happened since the
        // last tick and other players should stop seeing it as soon as possible), then the periodic 5 s
        // keep-alives, then the 60 s expiry sweep -- see each method's own remarks.
        DrainClaimedGroundItemDespawns();
        RebroadcastMonsters();
        RebroadcastGroundItems();
        ExpireGroundItems();
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
    ///     Drains <see cref="_inventoryInbox" />: applies each already-validated, already-SQL-durable
    ///     <see cref="InventoryZoneCommand" /> (<c>GenericActionHandler</c>) onto the live
    ///     <see cref="PlayerRuntimeState" /> -- the ONLY place <see cref="PlayerRuntimeState.Inventory" />/
    ///     <see cref="PlayerRuntimeState.Stats" /> are mutated after world entry, preserving the single-writer
    ///     invariant (architecture reference §10.1) exactly like <see cref="DrainInbox" /> does for position/
    ///     vitals. Kept as its OWN method/channel rather than a new <see cref="ZoneCommandKind" /> case: this
    ///     task's perimeter (V2 Inventory &amp; Equipment) is additive-only and must never edit
    ///     <see cref="DrainInbox" />'s existing switch.
    /// </summary>
    private void DrainInventoryCommands()
    {
        while (_inventoryInbox.Reader.TryRead(out var command))
            try
            {
                ApplyInventoryCommand(in command);
            }
            catch (Exception ex)
            {
                // Same containment posture as DrainInbox: one bad inventory command must never take the whole
                // tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} inventory command for character {CharacterId} failed", MapId,
                    command.CharacterId);
            }
    }

    /// <summary>
    ///     No validation, no I/O, no business logic here on purpose (see <see cref="InventoryZoneCommand" />'s
    ///     own remarks) -- everything was already decided and already persisted by the posting handler before
    ///     this ever reached the inbox. A no-op (no log) if the character already left this zone by the time
    ///     the tick drains this: their SQL write is already durable regardless, so there is nothing left to
    ///     mirror -- the exact same benign race <see cref="ApplyDeath" /> already accepts for a similarly-timed
    ///     disconnect.
    /// </summary>
    private void ApplyInventoryCommand(in InventoryZoneCommand command)
    {
        if (!_players.TryGetValue(command.CharacterId, out var state))
            return;

        foreach (var snapshot in command.Containers)
            state.Inventory.ReplaceContainer(snapshot.Container, snapshot.Slots);

        if (command.UpdatedStats is { } stats)
            state.Stats = stats;
    }

    private void DrainCombatCommands()
    {
        while (_combatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyCombatCommand(in command);
            }
            catch (Exception ex)
            {
                // Same containment posture as DrainInbox/DrainInventoryCommands: one bad attack must never take
                // the whole tick loop down for every other player in the zone.
                logger.LogError(ex, "Zone {MapId} combat command from character {CharacterId} failed", MapId,
                    command.AttackerCharacterId);
            }
    }

    /// <summary>
    ///     Resolves ONE CZ_PROCESS_ATTACK_SEND request (report 05 §4's <c>mCase</c> 1-6 dispatch) entirely on
    ///     this zone's own tick thread. Only <c>mCase</c> 2 (Avatar -&gt; Avatar, enemy tribe) is implemented
    ///     end-to-end today -- see <see cref="CombatResolver" />'s own remarks for exactly what is/isn't
    ///     modeled and why (<c>mCase</c> 1 needs a duel subsystem Fenrir doesn't have; 3/4 need monster entities
    ///     (V4); 5/6 need a stun subsystem). Every unimplemented case is a silent no-op here -- NOT a
    ///     disconnect: <c>AttackHandler</c> already rejected any <c>mCase</c> outside 1-6 (anti-fuzzing), so a
    ///     value inside that range but not yet wired up is a legitimate, in-progress feature, not a hostile
    ///     packet.
    /// </summary>
    private void ApplyCombatCommand(in CombatCommand command)
    {
        // V4 (Monsters & Loot): mCase 3 (Avatar -> Monster, ProcessAttack03) is now wired -- see
        // ApplyPvmAttack's own remarks. mCase 4 is deliberately NOT handled here even though a client
        // COULD send it: the legacy itself only ever reaches ProcessAttack04 from the monster's own AI
        // (S07_MyGame05.cpp:3961), never from this wire dispatch -- see Zone.ResolveMonsterAttack.
        if (command.AttackInfo.Case == 3)
        {
            ApplyPvmAttack(command);
            return;
        }

        if (command.AttackInfo.Case != 2)
            return; // mCase 1/4/5/6 -- deliberately unimplemented, see method remarks.

        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        SkillDefinition? attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                                       worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                                           out var skillDef)
            ? skillDef
            : null;

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0; // charge buff slot 8, value half -- single-use per report §4 pt.3.

        // "1 + attacker's weapon ItemId" on a hit (l.1360, AttackPlayer -- used client-side to pick the swing
        // animation/effect), 0 on a miss (l.1046) -- verified at both call sites.
        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        defenderState.Life -= outcome.DamageApplied;
        dirtyTracker.MarkDirty(defenderState.CharacterId, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill);
    }

    /// <summary>
    ///     mCase 3, "Avatar -&gt; Monster" (<c>ProcessAttack03</c>, report 05 §4) -- the V4 (Monsters &amp; Loot)
    ///     counterpart of <see cref="ApplyCombatCommand" />'s own mCase 2 branch above. Reuses
    ///     <see cref="TryDamageMonster" /> for the actual HP mutation/death handoff so there is exactly ONE
    ///     code path that ever decides "this monster just died" (the same one a future non-combat damage
    ///     source, if any, would also go through).
    /// </summary>
    private void ApplyPvmAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_monsters.TryGetValue(command.AttackInfo.ServerIndex2, out var monster))
            return;
        if (monster.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0; // charge buff slot 8, value half -- single-use, same convention as mCase 2

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { attackerState.CharacterId };
        foreach (var id in _grid.Neighbors(attackerState.CurrentCell)) recipients.Add(id);
        foreach (var id in NeighborsOfPosition(monster.PosX, monster.PosZ)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out _, out _);
    }

    private CombatantSnapshot ToCombatantSnapshot(PlayerRuntimeState state)
    {
        return new CombatantSnapshot(
            state.CharacterId,
            state.Tribe,
            state.IsDead,
            state.Life,
            state.MaxLife,
            state.PosX,
            state.PosY,
            state.PosZ,
            state.ZoneEntryAtZoneClock,
            state.Stats ?? default,
            state.Buffs.Buff[8 * 2]);
    }

    /// <summary>Attacker + defender + both their AOI neighbors, deduplicated -- matches the legacy's own "AOI broadcast + unicast to the attacker" (contract doc on <c>AttackResponse</c>).</summary>
    private HashSet<int> CombatRecipients(PlayerRuntimeState attacker, PlayerRuntimeState defender)
    {
        var recipients = new HashSet<int> { attacker.CharacterId, defender.CharacterId };
        foreach (var id in _grid.Neighbors(attacker.CurrentCell)) recipients.Add(id);
        foreach (var id in _grid.Neighbors(defender.CurrentCell)) recipients.Add(id);
        return recipients;
    }

    private void BroadcastAttackResult(IEnumerable<int> recipientCharacterIds, in AttackResponse response)
    {
        foreach (var id in recipientCharacterIds)
            try
            {
                if (_players.TryGetValue(id, out var recipient))
                    recipient.Session.Send(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} attack-result send to character {RecipientId} failed", MapId,
                    id);
            }
    }

    /// <summary>
    ///     XP hook Phase C/V4 (monster entities) calls once a monster's death resolves its killer -- kept
    ///     deliberately independent of any monster/combat type this pass doesn't have (mission's "clean seam,
    ///     do not block on V4"): a caller needs only the killer's character id and the two plain values already
    ///     on a monster template (<c>MonsterRowDto.RealLevel</c>/<c>GeneralExperience</c>). Formula: report 05
    ///     §5 <c>MONSTER_OBJECT::ProcessForExp</c>, ported in <see cref="ExperienceFormulas" /> (see that type's
    ///     remarks for the multipliers deliberately NOT modeled -- last-hit/teacher/party/event bonuses).
    /// </summary>
    public void GrantMonsterKillExperience(int killerCharacterId, int monsterLevel, int monsterGeneralExperience)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);
        var finalGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, state.Level);
        if (finalGain <= 0)
            return;

        state.Experience += finalGain;
        dirtyTracker.MarkDirty(killerCharacterId, DirtyFlags.Progression);
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
    ///     packet-lossy neighbors converge. Same wire packet as a move (<see cref="AvatarActionResponse" />),
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

    /// <summary>V4 keep-alive rebroadcast for monsters -- 5 s cadence (<see cref="LegacyTime.MonsterRebroadcastInterval" />, report 05 §0 item 7).</summary>
    private void RebroadcastMonsters()
    {
        foreach (var monster in _monsters.Values)
        {
            if (_clock - monster.LastRebroadcastAt < LegacyTime.MonsterRebroadcastInterval)
                continue;

            monster.LastRebroadcastAt = _clock;
            BroadcastMonsterAction(monster, 0);
        }
    }

    /// <summary>V4 keep-alive rebroadcast for ground items -- 5 s cadence (<see cref="LegacyTime.GroundItemRebroadcastInterval" />, report 05 §0 item 8).</summary>
    private void RebroadcastGroundItems()
    {
        foreach (var (index, item) in _groundItems)
        {
            var last = _groundItemLastRebroadcast.TryGetValue(index, out var t) ? t : TimeSpan.MinValue;
            if (_clock - last < LegacyTime.GroundItemRebroadcastInterval)
                continue;

            _groundItemLastRebroadcast[index] = _clock;
            BroadcastGroundItemAction(item, 0);
        }
    }

    /// <summary>60 s lifetime sweep (<see cref="LegacyTime.GroundItemLifetime" />, report 05 §5) -- despawns and broadcasts every expired ground item still present.</summary>
    private void ExpireGroundItems()
    {
        List<(int Index, GroundItemEntity Item)>? expired = null;
        foreach (var (index, item) in _groundItems)
            if (item.IsExpired(_clock))
                (expired ??= []).Add((index, item));

        if (expired is null)
            return;

        foreach (var (index, item) in expired)
            if (_groundItems.TryRemove(index, out _))
            {
                _groundItemLastRebroadcast.Remove(index);
                BroadcastGroundItemAction(item, 3); // report 05 §5: "expiration ... B_ITEM_ACTION_RECV(...,3)"
            }
    }

    /// <summary>Drains despawn broadcasts queued by a cross-thread <see cref="TryClaimGroundItem" /> success -- see <see cref="_claimedGroundItemDespawns" />'s remarks for why this can't broadcast inline.</summary>
    private void DrainClaimedGroundItemDespawns()
    {
        while (_claimedGroundItemDespawns.TryDequeue(out var item))
        {
            _groundItemLastRebroadcast.Remove(item.ServerIndex);
            BroadcastGroundItemAction(item, 3);
        }
    }

    /// <summary>Serialize-once broadcast for monster replication -- same pattern as <see cref="BroadcastAvatarAction" />.</summary>
    private void BroadcastMonsterAction(MonsterEntity monster, int checkChangeActionState)
    {
        var recipients = NeighborsOfPosition(monster.PosX, monster.PosZ).ToArray();
        if (recipients.Length == 0)
            return;

        var packet = BuildMonsterActionRecv(monster, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} monster broadcast to character {RecipientId} failed", MapId,
                        id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static MonsterReplicationResponse BuildMonsterActionRecv(MonsterEntity monster,
        int checkChangeActionState)
    {
        return new MonsterReplicationResponse
        {
            ServerIndex = monster.ServerIndex,
            UniqueNumber = monster.UniqueNumber,
            Data = new ObjectForMonster
            {
                Index = monster.Template.MonsterId,
                Action = new ActionInfo
                {
                    Type = 0,
                    Sort = (int)monster.AiState,
                    Frame = 0,
                    Location = [monster.PosX, monster.PosY, monster.PosZ],
                    TargetLocation = [monster.PosX, monster.PosY, monster.PosZ],
                    Front = monster.Heading,
                    TargetFront = monster.Heading,
                    PetLocation = new float[3],
                    PetTargetLocation = new float[3],
                    PetFront = 0,
                    PetSort = 0,
                    TargetObjectSort = 0,
                    TargetObjectIndex = monster.TargetCharacterId ?? 0,
                    TargetObjectUniqueNumber = 0,
                    SkillNumber = 0,
                    SkillGradeNum1 = 0,
                    SkillGradeNum2 = 0,
                    SkillValue = 0
                },
                LifeValue = monster.Life
            },
            CheckChangeActionState = checkChangeActionState
        };
    }

    /// <summary>Serialize-once broadcast for ground-item replication -- same pattern as <see cref="BroadcastAvatarAction" />.</summary>
    private void BroadcastGroundItemAction(GroundItemEntity item, int checkChangeActionState)
    {
        var recipients = NeighborsOfPosition(item.PosX, item.PosZ).ToArray();
        if (recipients.Length == 0)
            return;

        var packet = BuildItemActionRecv(item, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<GroundItemReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} ground-item broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static GroundItemReplicationResponse BuildItemActionRecv(GroundItemEntity item,
        int checkChangeActionState)
    {
        return new GroundItemReplicationResponse
        {
            ServerIndex = item.ServerIndex,
            UniqueNumber = item.UniqueNumber,
            Data = new ObjectForItem
            {
                Index = item.ItemId,
                Quantity = item.Quantity,
                Value = item.Value,
                SerialNumber = item.SerialNumber,
                Location = [item.PosX, item.PosY, item.PosZ],
                Master = item.Master,
                PartyName = item.PartyName,
                DropSort = item.DropSort,
                CreateTime = 0,
                PresentTime = 0,
                CreateState = 1,
                SocketGem = [item.SocketGem1, item.SocketGem2, item.SocketGem3]
            },
            CheckChangeActionState = checkChangeActionState
        };
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
            LastAvatarRebroadcastAt = _clock,
            // Carried through an in-process handoff (ZoneTransfer.CreateEnterData) so a player mid-death who
            // transfers zones before the auto-revive fires doesn't silently come back "alive" with 0 HP on
            // arrival -- defaults false for a fresh SQL-backed world entry, which is never mid-death (a login
            // is, by construction, a NEW session -- the legacy's own HP-force-to-1 register-time dance, report
            // 12 §4.2 step 4, is a distinct concern this DTO does not need to model: no persisted "IsDead"
            // column exists in game.Characters, since Vitals are not yet part of any write-behind flush -- see
            // this task's StructuredOutput openIssues).
            IsDead = data.IsDead,
            // This zone's OWN clock plus whatever remained of the timer in the SOURCE zone (data.ReviveRemaining,
            // already translated out of the source's absolute clock by ZoneTransfer.CreateEnterData) -- NOT a
            // fresh full delay, and NOT left at the type's own zero-default (which ProcessPendingRevives would
            // immediately treat as overdue) when the arriving player is mid-death.
            ReviveAtZoneClock = _clock + (data.ReviveRemaining ?? TimeSpan.Zero),
            // The ONLY write site for this field (PlayerRuntimeState.ZoneEntryAtZoneClock's own remarks): a
            // one-shot ~10s combat grace period starting THIS instant, for every arrival -- fresh world entry
            // AND an in-process zone-transfer handoff alike (arriving in a new zone is, from that zone's
            // perspective, exactly the "just spawned/just loaded" moment the legacy's own two write sites cover).
            // Combat code must never write this field again after today.
            ZoneEntryAtZoneClock = _clock
        };

        // Items/Stats are already-computed data handed down through the command (PlayerEnterData's own
        // remarks) -- this is a plain copy, never a catalog lookup or a StatCalculator call: those happened
        // in the poster (EnterWorldHandler for a fresh world entry, ZoneTransfer.CreateEnterData for
        // an in-process handoff), keeping this tick-thread method's cost independent of WorldDataCache size.
        if (data.Items is { } items)
            state.Inventory.Seed(items);
        if (data.Stats is { } stats)
            state.Stats = stats;

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
        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, _clock, handoffPosition);

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
    ///     XP penalty on death (report 05 §4/§6) is now applied HERE, but ONLY for <see cref="DeathCause.MonsterKill" />
    ///     -- report 05 §4 attributes the XP-loss formula EXCLUSIVELY to <c>ProcessAttack04</c> (a monster
    ///     killing the player); a PvP death instead rewards the KILLER via <c>ProcessForKillOtherTribe</c> (not
    ///     implemented, see <see cref="Combat.CombatResolver" />'s remarks) and does NOT dock the victim's XP --
    ///     see <see cref="DeathCause" />'s own remarks. Movement/interaction gating on <c>IsDead</c> (legacy
    ///     blocks potions and most actions while <c>aAction.aSort</c> is 11/stun or 12/death) is likewise left
    ///     for whichever handler needs it — this method only ever sets the flag.
    /// </remarks>
    public void ApplyDeath(int characterId, DeathCause cause = DeathCause.Unknown)
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
        state.ReviveAtZoneClock = _clock + LegacyTime.DeathReviveDelay;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);

        if (cause == DeathCause.MonsterKill)
            ApplyDeathExperienceLoss(state);

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
    ///     The MvP XP-loss branch of <see cref="ApplyDeath" /> (report 05 §4, S07_MyGame02.cpp:3445-3489):
    ///     refuses below level 10 or at/above the level cap (loses CP instead there -- <see cref="ExperienceFormulas.CpLossAtLevelCap" />).
    ///     <see cref="ExperienceFormulas.ComputeDeathExperienceLoss" /> needs <c>ReturnLevelFactor1(level)</c> =
    ///     <c>world.Levels[level].ExpRangeMin</c> -- a level outside the catalog (data gap) contributes 0 (no loss),
    ///     the same "absent contributes nothing" posture <see cref="Stats.StatCalculator" /> already applies.
    /// </summary>
    private void ApplyDeathExperienceLoss(PlayerRuntimeState state)
    {
        if (state.Level < ExperienceFormulas.MinimumLevelForDeathExperienceLoss)
            return;

        if (state.Level >= ExperienceFormulas.MaxLimitLevel)
        {
            state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
            dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
            return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin);
        if (loss <= 0)
            return;

        state.Experience -= loss;
        dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Progression);
    }

    /// <summary>
    ///     Sweeps every dead player whose scheduled revive (<see cref="ApplyDeath" />) is due, in this frame's
    ///     tick. Due entries are snapshotted into a small list first (allocated only on a tick with at least
    ///     one due revive, the rare case) purely to keep the enumeration pattern consistent with any future
    ///     revive step that might need to mutate <see cref="_players" />; <see cref="Revive" /> itself never
    ///     removes an entry today.
    /// </summary>
    private void ProcessPendingRevives()
    {
        List<(int CharacterId, PlayerRuntimeState State)>? due = null;

        foreach (var (characterId, state) in _players)
        {
            if (!state.IsDead || _clock < state.ReviveAtZoneClock)
                continue;

            (due ??= []).Add((characterId, state));
        }

        if (due is null)
            return;

        foreach (var (characterId, state) in due)
            Revive(characterId, state);
    }

    /// <summary>
    ///     Executes a due revive resolved by <see cref="ApplyDeath" />: HP forced to 1 regardless of MaxLife
    ///     (report 12 §4.2, matching the legacy <c>REGISTER_AVATAR_SEND</c> flow's own force-to-1), IN PLACE --
    ///     same zone, same position. This mirrors the legacy's own documented behavior (report 12 §4.2/§4.3):
    ///     after the delay, the server only auto-clears the death flag locally ("le client peut se relever
    ///     localement, il n'envoie pas de paquet de résurrection dédié") -- an actual cross-zone "return to
    ///     town" transfer is ALWAYS client-driven (CZ_DEMAND_ZONE_SERVER_INFO_2, Sort=3, the client's own
    ///     chosen destination zone number), already fully handled by the existing, direction-agnostic
    ///     <c>ZoneMoveHandler</c> (which works whether the player is dead or
    ///     alive). An earlier pass here had this auto-timer ALSO perform an unconditional cross-zone teleport to
    ///     a hardcoded tribe capital -- removed: it both diverged from the documented legacy behavior and (worse)
    ///     dropped the destination silently to zone/position 0 for any player handed off to another zone while
    ///     still dead (no field carried a pending cross-zone revive through <see cref="ZoneTransfer.CreateEnterData" />),
    ///     causing the auto-timer to misfire on arrival. Reviving strictly in place removes that whole class of
    ///     bug by construction: there is no cross-zone state left to lose.
    /// </summary>
    private void Revive(int characterId, PlayerRuntimeState state)
    {
        state.IsDead = false;
        state.Life = 1;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Vitals);

        SendAvatarAction(state.Session, state);
        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
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

        // Mirrors the legacy's persistent mDATA.aAction fields (report 05 §7, 12 §4.2) for EVERY accepted
        // action, not just plain movement -- sit/meditation (Sort=31, MeditationRegenSystem) and skill casts
        // (Sort=30, ApplySkillCast below) ride the same unified CZ_AVATAR_ACTION_SEND wire shape.
        state.ActionSort = action.Sort;
        state.ActionSkillNumber = action.SkillNumber;
        state.ActionSkillGradeNum1 = action.SkillGradeNum1;
        state.ActionSkillGradeNum2 = action.SkillGradeNum2;

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Self is excluded: the legacy client applies its own movement locally (client-side prediction) and
        // does not need its own action echoed back to it.
        var neighbors = _grid.Neighbors(newCell).Where(id => id != characterId).ToArray();
        BroadcastAvatarAction(neighbors, state, action);

        if (action.Sort == 30)
            ApplySkillCast(state, action);
    }

    /// <summary>
    ///     Non-attack skill cast (report 12 §4.2: <c>AVATAR_ACTION_SEND</c> Sort=30 = "cast de skill buff").
    ///     Damage-dealing skills do NOT go through here -- those ride <c>CZ_PROCESS_ATTACK_SEND</c>'s
    ///     <c>AttackActionValue1==2</c> path (<see cref="ApplyCombatCommand" />/<see cref="CombatResolver" />).
    ///     A silent no-op on every failure path (unknown skill, insufficient mana, wrong weapon class, cooldown)
    ///     -- exactly the legacy's own bare early-<c>return FALSE</c> contract (no dedicated failure packet).
    /// </summary>
    private void ApplySkillCast(PlayerRuntimeState state, ActionInfo action)
    {
        // One skill-cast per legacy tick, modeled after the verified USE_INVENTORY_ITEM anti-flood gate
        // (report 04 §2) -- see LastSkillCastAtZoneClock's own remarks for why this specific analog was chosen
        // over inventing a per-skill cooldown value no report documents. Null (never cast) always passes.
        if (state.LastSkillCastAtZoneClock is { } lastCast && _clock - lastCast < LegacyTime.LegacyTick)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, gradePoints, state.Mana, maxLife, weaponSort);
        if (!result.Success)
            return;

        state.LastSkillCastAtZoneClock = _clock;
        state.Mana -= result.ManaCost;
        dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Vitals);

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                ApplySkillBuffWrites(state, result.BuffWrites);
                break;
            case SkillEffectKind.HealLife:
                ApplyTargetedHeal(action, isLife: true, result.HealAmount);
                break;
            case SkillEffectKind.HealMana:
                ApplyTargetedHeal(action, isLife: false, result.HealAmount);
                break;
        }
    }

    private void ApplySkillBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = new int[35];
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35) continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    /// <summary>
    ///     Targeted heal (skills 106-111): resolves <see cref="ActionInfo.TargetObjectIndex" />/
    ///     <see cref="ActionInfo.TargetObjectUniqueNumber" /> against this SAME zone (Fenrir's UniqueNumber
    ///     convention is the plain CharacterId, <see cref="PlayerRuntimeState.UniqueNumber" />'s own remarks),
    ///     clamps the flat heal amount to the target's remaining capacity, exactly like the legacy's own
    ///     call site (S07_MyGame03.cpp:9500-9510/9563-9573) -- a target at full HP/MP, or not found/dead/hiding,
    ///     silently receives nothing (no dedicated failure packet in the legacy either).
    /// </summary>
    private void ApplyTargetedHeal(ActionInfo action, bool isLife, int rawAmount)
    {
        if (rawAmount < 1)
            return;
        if (!_players.TryGetValue(action.TargetObjectIndex, out var target))
            return;
        if (target.UniqueNumber != unchecked((uint)action.TargetObjectUniqueNumber))
            return;
        if (target.IsDead)
            return;

        if (isLife)
        {
            var max = target.Stats?.MaxLife ?? target.MaxLife;
            var amount = Math.Min(rawAmount, max - target.Life);
            if (amount < 1) return;
            target.Life += amount;
        }
        else
        {
            var max = target.Stats?.MaxMana ?? target.MaxMana;
            var amount = Math.Min(rawAmount, max - target.Mana);
            if (amount < 1) return;
            target.Mana += amount;
        }

        dirtyTracker.MarkDirty(target.CharacterId, DirtyFlags.Vitals);
    }

    /// <summary>
    ///     Recomputes <see cref="PlayerRuntimeState.Stats" /> from the live Equipment container + the CURRENT
    ///     <see cref="PlayerRuntimeState.Buffs" /> snapshot (a buff was just applied or
    ///     <see cref="Simulation.BuffExpirySystem" /> just expired one) and broadcasts the updated buff view
    ///     (<c>ZC_AVATAR_EFFECT_VALUE_INFO</c>) to this player and their AOI neighbors. Fenrir-specific
    ///     necessity, not a 1:1 legacy transcription: unlike the legacy's live-read <c>Get*</c> wrappers,
    ///     <see cref="PlayerRuntimeState.Stats" /> is an explicit, event-driven CACHE (report 11's own
    ///     <see cref="Stats.StatCalculator" /> remarks) that must be refreshed on every buff change to stay correct.
    /// </summary>
    internal void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.Title, state.Halo, state.RebirthCount);
        state.Stats = EquipmentService.RecomputeStats(attributes,
            state.Inventory.GetContainer(ContainerMatrix.Equipment), worldData, state.Buffs);

        var response = new AvatarEffectStateResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            EffectValue = state.Buffs.Buff,
            EffectValueState = changedSlots
        };

        state.Session.Send(response);
        foreach (var neighborId in _grid.Neighbors(state.CurrentCell))
        {
            if (neighborId == state.CharacterId) continue;
            if (_players.TryGetValue(neighborId, out var neighbor))
                neighbor.Session.Send(response);
        }
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
        var total = FrameWriter.FrameSizeOf<AvatarActionResponse>();
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

    private static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state)
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
    ///     Internal (not private): reused by <c>ZoneMoveHandler</c> to build the self-spawn
    ///     packet for a zone-transfer's fresh world-state push, with an explicit <paramref name="action" />
    ///     carrying the just-resolved ARRIVAL position rather than <paramref name="state" />'s own (still the
    ///     source zone's, single-writer invariant preserved -- see that handler's remarks).
    /// </summary>
    internal static AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new AvatarActionResponse
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
                // Reflects the live Equipment container instead of a hardcoded blank -- see
                // EquipmentViewCodec's own remarks (shared with EnterWorldHandler's self-spawn).
                EquipForView = EquipmentViewCodec.BuildEquipForView(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
                AnimalNumber = 0,
                Title = state.Title,
                Halo = state.Halo,
                RebirthNum = state.RebirthCount,
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
