using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     One zone actor per hosted map. Every player position, the AOI grid, and the dirty-tracker marking are
///     touched only from this zone's tick (<see cref="RunAsync" /> -&gt; <see cref="Tick" />) -- everything else
///     only ever calls <see cref="Post" /> and waits for the next tick.
/// </summary>
/// <remarks>
///     The tick runs in stages: drain inbox -&gt; simulate (whole 500 ms legacy ticks via
///     <see cref="SimulationTickAccumulator" />) -&gt; periodic keep-alive rebroadcast (avatars every 3.5 s,
///     monsters/items every 5 s, then the proxy-shop sweep, per-shop every 5 s, see
///     <see cref="SimulationClock" /> and <c>Zone.ProxyShops.cs</c>).
/// </remarks>
/// <remarks>
///     This class is split across several partial files by concern, all in this same folder:
///     <c>Zone.Monsters.cs</c> (monster spawn/damage/death + monster-kill money grants),
///     <c>Zone.GroundItems.cs</c> (ground-item spawn/claim/expiry), <c>Zone.Combat.cs</c> (attack resolution +
///     kill hooks), <c>Zone.Combat.DamagePipeline.cs</c> (the B15 cross-avatar depth terms -- destroyer roll,
///     reflect, Holy-Shield absorption -- shared by <c>Zone.Combat.cs</c>'s mCase-2 and <c>Zone.Duel.cs</c>'s
///     mCase-1 paths), <c>Zone.Chat.cs</c> (local/shout/tribe chat), <c>Zone.PlayerLifecycle.cs</c>
///     (enter/leave/move/death/revive/skill-cast + avatar-action broadcast), <c>Zone.EconomyMirrors.cs</c>
///     (already-SQL-durable inventory/skill/mentor/guild/tribe/quest/mission mirrors),
///     <c>Zone.CosmeticMirrors.cs</c> (drink-bottle/hero-ranking/fishing/mount/costume/stellar-core/avatar-buff/
///     rune-socket/auto-buff/pshop mirrors), <c>Zone.ProxyShops.cs</c> (the offline/deputy shop periodic
///     radius rebroadcast + expiry force-close sweep), <c>Zone.TribeBankTax.cs</c> (the 1%/9% tribe-bank
///     income tax accumulator and its 10-minute sweep), <c>Zone.Gm.cs</c> (the Elevated-tier GM-EXP
///     command's own read-decide-mutate mirror, the one GM-command concern that cannot be folded into
///     <c>Zone.EconomyMirrors.cs</c>'s already-decided-field-mirror shape), and <c>Zone.GuildBuffExpiry.cs</c>
///     (the guild-buff-reserve-exhaustion immediate strip-effect push, posted by
///     <c>Hosting.Guilds.GuildBuffDecayHost</c>/<c>Hosting.GuildBuffExpiryRelayHost</c> rather than by any
///     opcode handler). This file keeps the constructor, the fields/state
///     shared across several of those concerns (the AOI grid, the player map, the tick clock, RNG), and the
///     tick loop itself.
/// </remarks>
public sealed partial class Zone(
    short mapId,
    GameServerOptions options,
    MovementRules movementRules,
    DirtyTracker<int> dirtyTracker,
    IReadOnlyList<ISimulationSystem> simulationSystems,
    ILogger<Zone> logger,
    WorldDataCache worldData,
    IRandomSource? randomSource = null,
    QuestCatalog? questCatalog = null,
    KillCooldownTracker? killCooldownTracker = null,
    TowerWarState? towerWar = null,
    WorldStateService? worldState = null,
    PartyRegistry? partyRegistry = null,
    DuelRegistry? duelRegistry = null,
    TradeRegistry? tradeRegistry = null,
    HeroRankPointAccumulator? heroRankPointAccumulator = null,
    ICharacterShardLocationRepository? characterShardLocations = null,
    TribeBankTaxAccumulator? tribeBankTax = null,
    RegularWarActiveMapTracker? regularWarActiveMapTracker = null,
    ZoneRegistry? zoneRegistry = null,
    ZoneGeometry? geometry = null) : IZoneActor
{
    /// <summary>Bounded capacity for <see cref="_inbox" /> -- also the basis for <see cref="InboxDrainCapPerTick" />.</summary>
    private const int InboxCapacity = 8192;

    /// <summary>
    ///     Per-tick cap on how many items <see cref="DrainInbox" /> will read from <see cref="_inbox" /> before
    ///     returning control to the rest of <see cref="Tick" /> (Simulate/Rebroadcast): half of
    ///     <see cref="InboxCapacity" />. Every other <c>Drain*Commands</c> method across this partial class
    ///     family (<c>Zone.Combat.cs</c>, <c>Zone.Chat.cs</c>, <c>Zone.EconomyMirrors.cs</c>,
    ///     <c>Zone.CosmeticMirrors.cs</c>) applies this same "half of that channel's own bounded capacity"
    ///     convention to its own channel.
    ///     <para>
    ///         Without a cap, a channel-saturating burst (a client bug, a lag spike driving a flood of retries,
    ///         or simply a very active zone filling a channel up to its bound) is fully drained inline in a
    ///         single <see cref="Tick" /> call, however large, before Simulate/Rebroadcast run for that frame at
    ///         all. Capping at half of the channel's own bound instead spreads a full-to-capacity backlog across
    ///         at least two ticks, while any realistic normal load -- nowhere near a channel's own bound -- is
    ///         completely unaffected (the cap can only ever bind once the backlog already exceeds half of what
    ///         the channel is willing to hold in the first place). Any remainder simply stays queued in the
    ///         channel; the next <see cref="Tick" /> drains it from where this one left off -- nothing is
    ///         dropped or reordered by this cap itself.
    ///     </para>
    ///     <para>
    ///         This is a Fenrir-side robustness safeguard, not a legacy-parity reproduction: no
    ///         <c>Server/path:line</c> citation was supplied or independently located for an equivalent
    ///         per-frame processing cap in the legacy <c>ts25zone</c> message pump this tick loop is modeled
    ///         after -- see the <c>fenrir-tick-loop-self-critique</c> finding/behavior contract this constant
    ///         was added for.
    ///     </para>
    /// </summary>
    private const int InboxDrainCapPerTick = InboxCapacity / 2;

    private readonly SimulationTickAccumulator _accumulator = new();

    /// <summary>
    ///     Process-wide 1v1 duel authority (the stun request's duel-exception tribe gate,
    ///     <see cref="ApplyStunAttack" />) -- defaults to a private instance in tests, same posture as
    ///     <see cref="_partyRegistry" />.
    /// </summary>
    private readonly DuelRegistry _duelRegistry = duelRegistry ?? new DuelRegistry();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    /// <summary>
    ///     Process-wide write-behind for hero-rank points granted by <see cref="ApplyPvpKillHeroPoints" /> --
    ///     defaults to a private instance in tests, same posture as <see cref="_partyRegistry" />.
    /// </summary>
    private readonly HeroRankPointAccumulator _heroRankPointAccumulator =
        heroRankPointAccumulator ?? new HeroRankPointAccumulator();

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(InboxCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    /// <summary>
    ///     Reusable scratch buffer backing <see cref="NeighborsOfPosition" />'s result -- <see cref="MonsterAiSystem" />
    ///     is a single DI singleton shared across every zone this process ticks (see that class's own remarks),
    ///     so it cannot safely own this buffer itself; only <see cref="Zone" /> is guaranteed confined to a
    ///     single tick thread. Repopulated fresh on every <see cref="NeighborsOfPosition" /> call and valid only
    ///     until the next one -- <see cref="Monsters.MonsterAiSystem.TryAcquireTarget" />, the sole caller,
    ///     fully consumes it in its own immediately-following <c>foreach</c> before returning, so no reentrant
    ///     use can observe a half-built or already-cleared buffer.
    /// </summary>
    private readonly List<int> _monsterAiNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer backing <see cref="BorrowBossAggroScratch" /> -- the per-acquisition-tick
    ///     aggro candidate list a Zone175-type boss's multi-candidate acquisition
    ///     (<see cref="Monsters.MonsterAiSystem" />) fills and then consumes within one decision pass. Same
    ///     single-tick-thread confinement rationale as <see cref="_monsterAiNeighborScratch" />: the shared
    ///     <see cref="Monsters.MonsterAiSystem" /> singleton cannot own it, only <see cref="Zone" /> is
    ///     guaranteed one tick thread. Cleared on every borrow, valid only until the next borrow.
    /// </summary>
    private readonly List<Monsters.MonsterAggroCandidate> _monsterBossAggroScratch = [];

    /// <summary>
    ///     Process-wide party authority (team-stun's exact-5-member gate, <see cref="ApplyStunAttack" />) --
    ///     defaults to a private instance in tests so each test zone starts with a clean, empty party roster.
    /// </summary>
    private readonly PartyRegistry _partyRegistry = partyRegistry ?? new PartyRegistry();

    // ConcurrentDictionary, not a plain Dictionary: the tick is the sole writer, but the write-behind flush
    // callback and the directory-heartbeat CCU count both read this from other threads.
    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

    /// <summary>Combat/skill RNG -- <see cref="SystemRandomSource" /> in production, injectable for deterministic tests.</summary>
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

    /// <summary>
    ///     Process-wide secure-trade authority (disconnect-cleanup half of the trade lifecycle,
    ///     <see cref="ClearTradeOnDisconnect" />) -- defaults to a private instance in tests, same posture as
    ///     <see cref="_duelRegistry" />/<see cref="_partyRegistry" />.
    /// </summary>
    private readonly TradeRegistry _tradeRegistry = tradeRegistry ?? new TradeRegistry();

    /// <summary>
    ///     This zone's own owning registry -- used only to resolve OTHER zones' tracked players when this zone's
    ///     tick needs to reach someone outside its own <see cref="_players" /> (currently:
    ///     <see cref="BreakPartyOnDisconnect" />'s cross-zone party-changed broadcast). Passed as <c>this</c> by
    ///     <see cref="ZoneRegistry.Initialize" /> when it builds every <see cref="Zone" /> instance -- safe with
    ///     no <see cref="Monsters.MonsterSpawnScheduler" />-style DI-container constructor cycle to defer around,
    ///     since <see cref="Zone" /> is never itself DI-resolved: it is only ever built imperatively by
    ///     <see cref="ZoneRegistry.Initialize" />, which cannot run until the <see cref="ZoneRegistry" /> singleton
    ///     it is a method on already fully exists. Null only in test call sites that construct a single
    ///     <see cref="Zone" /> directly -- those degrade to same-zone-only notification, same posture as every
    ///     other optional cross-cutting dependency here.
    /// </summary>
    private readonly ZoneRegistry? _zoneRegistry = zoneRegistry;

    /// <summary>
    ///     This zone's own monotonic simulated clock, the sum of every elapsed span fed to <see cref="Tick" />.
    ///     Periodic cadences are measured against this, not wall clock, so a test can drive simulated hours
    ///     through <see cref="Tick" /> in microseconds.
    /// </summary>
    private TimeSpan _clock;

    /// <summary>The legacy map this actor simulates -- its key in <see cref="ZoneRegistry" />.</summary>
    public short MapId { get; } = mapId;

    public int PlayerCount => _players.Count;

    /// <summary>
    ///     Read-only enumeration for <see cref="ISimulationSystem" />s running on this zone's own tick thread. A
    ///     system may mutate the yielded <see cref="PlayerRuntimeState" /> instances directly, but must never
    ///     add/remove entries here.
    /// </summary>
    public IEnumerable<PlayerRuntimeState> Players => _players.Values;

    /// <summary>
    ///     Consumed by <see cref="HandleMove" /> via <see cref="MovementRules.IsPlausible" /> for terrain-aware
    ///     movement validation. Null (logged, not a startup crash) when the <c>.WM</c> file is absent -- the
    ///     legacy game-data tree is an external asset not committed to the repo, so its absence must not block
    ///     the zone from ticking; validation degrades to speed-only in that case.
    ///     <para>
    ///         The <c>geometry</c> constructor parameter is a test seam: when supplied it replaces the file load,
    ///         letting a test drive terrain-aware movement/pathfinding over a hand-built mesh without an on-disk
    ///         <c>.WM</c>. Null in every production call site (<see cref="ZoneRegistry.Initialize" />), which
    ///         always loads from disk.
    ///     </para>
    /// </summary>
    public ZoneGeometry? Geometry { get; } = geometry ?? TryLoadGeometry(mapId, options, logger);

    /// <summary>
    ///     Lazily-built A* + funnel navmesh router for monster movement (<see cref="Monsters.MonsterAiSystem" />),
    ///     tick-thread-only. Null when <see cref="Geometry" /> is absent (movement degrades to unconstrained
    ///     straight-line). Created on the first monster path request rather than at construction so a zone with
    ///     geometry but no aggressive, actually-pathing monster never pays the triangle-adjacency build cost --
    ///     see <see cref="Pathfinder" />.
    /// </summary>
    private MonsterPathfinder? _pathfinder;

    /// <summary>
    ///     Tick-thread-only accessor that lazily creates <see cref="_pathfinder" /> on first use. Returns null
    ///     (no pathfinding, straight-line fallback) when this zone has no loaded <see cref="Geometry" />.
    /// </summary>
    public MonsterPathfinder? Pathfinder =>
        Geometry is null
            ? null
            : _pathfinder ??= new MonsterPathfinder(Geometry, options.MonsterPathfindingBudgetPerTick);

    /// <summary>
    ///     Resets this tick's monster-pathfinding budget -- called once per <see cref="Monsters.MonsterAiSystem" />
    ///     pass before its monster loop. Deliberately resets only an <em>already-built</em> pathfinder: it must
    ///     not force <see cref="Pathfinder" />'s lazy build (and the adjacency-graph cost behind it) on a zone
    ///     whose monsters have never actually pathed. The first real path request starts with a full budget from
    ///     the pathfinder's own constructor, so skipping the reset before that point is correct.
    /// </summary>
    public void ResetPathBudget()
    {
        _pathfinder?.ResetBudget();
    }

    /// <summary>
    ///     Enqueues a command for the next tick. Never blocks: a full inbox drops the write rather than stall
    ///     whichever session thread posted it -- a dropped Move is simply superseded by the client's next one.
    /// </summary>
    public bool Post(in ZoneCommand command)
    {
        return _inbox.Writer.TryWrite(command);
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

    /// <summary>
    ///     Tick-thread-only: <see cref="_grid" /> itself is not thread-safe. Deliberately coarse-only (no
    ///     exact-distance filtering) -- <see cref="Monsters.MonsterAiSystem.TryAcquireTarget" /> is this
    ///     method's other caller, and its own detection radius (<c>MonsterRowDto.RadiusInfo2</c>) is an
    ///     independent, data-driven value that can exceed <see cref="GameServerOptions.AoiCellSize" />; that
    ///     call site applies its own exact-distance check afterward, so baking a fixed AOI-scale exact filter
    ///     in here would silently truncate detection range for any monster configured with a wider radius. A
    ///     broadcast call site that DOES want the AOI exact-distance pass (legacy's own <c>Broadcast11</c>
    ///     semantics, see <see cref="AoiGrid" />'s remarks) should query <see cref="_grid" /> directly instead
    ///     of going through this helper.
    ///     <para>
    ///         Returns the concrete <see cref="_monsterAiNeighborScratch" /> buffer, not <see cref="IEnumerable{T}" />
    ///         -- returning the interface would force the caller's <c>foreach</c> to box <see cref="List{T}" />'s
    ///         own value-type enumerator on every detection-throttle tick for every monster in the zone. This
    ///         also replaces the previous <c>yield return</c> iterator (a fresh compiler-generated enumerator
    ///         allocated per call) with a reused buffer -- see this field's own remarks for why the buffer lives
    ///         here rather than on the caller.
    ///     </para>
    /// </summary>
    public List<int> NeighborsOfPosition(float x, float z)
    {
        _monsterAiNeighborScratch.Clear();
        _grid.Neighbors(_monsterAiNeighborScratch, _grid.CellOf(x, z));
        return _monsterAiNeighborScratch;
    }

    /// <summary>
    ///     Tick-thread-only. Hands the caller the cleared, reusable <see cref="_monsterBossAggroScratch" />
    ///     buffer for a Zone175-type boss's multi-candidate acquisition (<see cref="Monsters.MonsterAiSystem" />)
    ///     to fill and then read within the same decision pass -- a non-allocating counterpart to
    ///     <see cref="NeighborsOfPosition" /> for a candidate list that carries per-entry distance/position, not
    ///     just an id. Distinct buffer from <see cref="_monsterAiNeighborScratch" /> so the acquisition loop can
    ///     iterate the neighbor-id list and populate this one at the same time without aliasing.
    /// </summary>
    public List<Monsters.MonsterAggroCandidate> BorrowBossAggroScratch()
    {
        _monsterBossAggroScratch.Clear();
        return _monsterBossAggroScratch;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromMilliseconds(1000.0 / options.TickRateHz);
        using var timer = new PeriodicTimer(tickInterval);

        // Real elapsed time between frames is measured, not assumed to equal tickInterval: PeriodicTimer
        // coalesces missed periods, and the SimulationTickAccumulator must be paid in actual time or the 2 Hz
        // simulation would silently slow down under load.
        var lastFrame = Stopwatch.GetTimestamp();

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(lastFrame, now);
            lastFrame = now;

            try
            {
                Tick(elapsed);
            }
            catch (Exception ex)
            {
                // Mirrors legacy's __try/__except around the ENTIRE per-tick Logic() call
                // (Server/ts25zone/S02_MyServer.cpp:412-431): any fault anywhere in this tick's processing is
                // contained here, logged, and only this one tick's remaining work is lost -- this zone's loop
                // resumes normally on the very next iteration, and every OTHER zone's loop (a separate task,
                // see ZoneTickHost) is completely unaffected either way. Every stage inside Tick already
                // carries its own finer-grained log-and-continue containment (DrainInbox, Simulate, and the
                // tail-stage guards below); this is the last-resort backstop for anything that still escapes
                // one of those (e.g. the clock/metrics bookkeeping in Tick itself), per the
                // zone-tick-loop-fault-isolation behavior contract.
                logger.LogError(ex, "Zone {MapId} tick failed; skipping the remainder of this tick", MapId);
            }

            var tickMs = Stopwatch.GetElapsedTime(now).TotalMilliseconds;
            if (tickMs > tickInterval.TotalMilliseconds)
                logger.LogWarning("Zone {MapId} tick took {ElapsedMs:F1} ms (budget {BudgetMs:F1} ms)", MapId,
                    tickMs, tickInterval.TotalMilliseconds);
        }
    }

    /// <summary>
    ///     One network frame of this zone: drain inbox -&gt; simulate due legacy ticks -&gt; periodic rebroadcast.
    ///     Public, but with exactly two legitimate callers -- <see cref="RunAsync" />'s timer loop, and tests
    ///     driving deterministic simulated time. Calling it from any other thread while <see cref="RunAsync" />
    ///     runs would break the single-writer invariant.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        _clock += elapsed;

        var t0 = Stopwatch.GetTimestamp();
        DrainInbox();
        DrainInventoryCommands();
        DrainSkillCommands();
        DrainCombatCommands();
        DrainChatCommands();
        DrainMentorCommands();
        DrainQuestCommands();
        DrainGuildCommands();
        DrainTribeProgressCommands();
        DrainGmExperienceCommands();
        DrainPshopCommands();
        DrainMissionCommands();
        DrainDrinkBottleCommands();
        DrainHotkeySlotMirrorCommands();
        DrainHeroRankingQueryCommands();
        DrainHeroRankingRolloverCommands();
        DrainFishingCommands();
        DrainMountCommands();
        DrainCostumeCommands();
        DrainStellarCoreCommands();
        DrainAvatarBuffCommands();
        DrainRuneSocketCommands();
        DrainAutoBuffCommands();
        DrainGuildBuffExpiryCommands();
        var t1 = Stopwatch.GetTimestamp();
        var legacyTicksElapsed = _accumulator.Advance(elapsed);
        Simulate(legacyTicksElapsed);
        var t2 = Stopwatch.GetTimestamp();

        // Claimed-item despawns first (so other players stop seeing it ASAP), then the 5 s keep-alives, then
        // the 60 s expiry sweep -- runs every frame regardless of the gates below, exactly as before. Each
        // stage below is individually try/caught, log-and-continue -- same posture as DrainInbox's per-command
        // guard and Simulate's per-system guard just above: a fault in any ONE tail stage must never skip the
        // REMAINING tail stages, and must never escape Tick itself (zone-tick-loop-fault-isolation behavior
        // contract; legacy's own coarser __try/__except around the whole per-tick Logic() call is the ancestor
        // of this same guarantee, Server/ts25zone/S02_MyServer.cpp:412-431).
        try
        {
            DrainClaimedGroundItemDespawns();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                nameof(DrainClaimedGroundItemDespawns));
        }

        var hasPlayers = !_players.IsEmpty;

        // Legacy throttled avatar/monster/item keep-alive rebroadcasts and the ground-item expiry sweep to
        // its own ~2 Hz tick (TimeLogic=500ms); Fenrir's 20 Hz network frame is 10x finer-grained than that,
        // so gating this block on "at least one legacy tick elapsed" matches legacy's own cadence instead of
        // redoing this work on 9 out of 10 frames for nothing. Every threshold this touches (3.5 s/5 s/5 s/
        // 60 s) is at least 7x the legacy tick, so the worst-case added latency (~450 ms) is imperceptible
        // against any of them. RebroadcastProxyShops is deliberately NOT included here even though it is also
        // a periodic rebroadcast: its own force-close branch is documented as unconditional on any throttle
        // (an expired shop must close the very first tick this sweep observes it), so it keeps running every
        // frame, same as before.
        if (legacyTicksElapsed > 0)
        {
            try
            {
                RebroadcastAvatars();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(RebroadcastAvatars));
            }

            // A zone with nobody connected has an empty AOI grid -- no possible recipient anywhere in this
            // zone for a monster/ground-item keep-alive, so these two calls (pure broadcast, no other side
            // effect) are free to skip. ExpireGroundItems is deliberately NOT population-gated: it performs
            // a real state mutation (removing expired items from this zone), not just a broadcast.
            if (hasPlayers)
            {
                try
                {
                    RebroadcastMonsters();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                        nameof(RebroadcastMonsters));
                }

                try
                {
                    RebroadcastGroundItems();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                        nameof(RebroadcastGroundItems));
                }
            }

            try
            {
                ExpireGroundItems();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(ExpireGroundItems));
            }
        }

        try
        {
            RebroadcastProxyShops();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(RebroadcastProxyShops));
        }

        // Unlike RebroadcastProxyShops just above (deliberately unconditional -- see that block's own
        // comment), this subsystem has its own documented 20-LEGACY-TICK status-broadcast cadence
        // (PersonalDungeonBattleBroadcastCadenceTicks, Zone.DungeonInstance.cs) that must be paid in legacy
        // ticks, not physical 20 Hz frames -- gating it here the same way as the RebroadcastAvatars/etc.
        // block above (and feeding it legacyTicksElapsed, so a stalled host's multi-tick catch-up still
        // advances the per-player counter correctly instead of just +1) keeps its cadence at the documented
        // ~10 s rather than firing ~10x too often.
        if (legacyTicksElapsed > 0)
            try
            {
                AdvanceZone241PersonalDungeonInstances(legacyTicksElapsed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                    nameof(AdvanceZone241PersonalDungeonInstances));
            }

        // TryFlushTribeBankTax's own correct cadence relative to legacy is an open question (not resolved
        // by the fenrir-tick-loop-self-critique finding this file's dungeon-instance gate above was fixed
        // from) -- left unconditional here rather than guessing a gate for it. TrySweep itself is
        // wall-clock-driven (_clock vs. a 10-minute threshold, not a tick counter), so calling it up to 10x
        // more often than legacy's own tick only means checking that clock comparison more often, not a
        // correctness issue the way the ungated dungeon-instance counter above was.
        try
        {
            TryFlushTribeBankTax();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(TryFlushTribeBankTax));
        }

        var t3 = Stopwatch.GetTimestamp();

        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.DrainStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.SimulateStage);
        ZoneTickMetrics.StageDurationMs.Record(Stopwatch.GetElapsedTime(t2, t3).TotalMilliseconds, _mapTag,
            ZoneTickMetrics.RebroadcastStage,
            hasPlayers ? ZoneTickMetrics.PopulationActiveTag : ZoneTickMetrics.PopulationIdleTag);
    }

    private void DrainInbox()
    {
        var processed = 0;
        while (processed < InboxDrainCapPerTick && _inbox.Reader.TryRead(out var command))
        {
            processed++;
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
                        HandleMove(command.CharacterId, in action, command.IsResumeAction);
                        break;
                    case ZoneCommandKind.PetAction:
                        var petAction = command.Action;
                        HandlePetAction(command.CharacterId, in petAction);
                        break;
                    case ZoneCommandKind.MarkZoneTransferPending:
                        HandleMarkZoneTransferPending(command.CharacterId);
                        break;
                    case ZoneCommandKind.SetMuted:
                        HandleSetMuted(command.CharacterId, command.Muted);
                        break;
                    case ZoneCommandKind.CreditRegularWarConclusion:
                        HandleRegularWarConclusionCredit(command.CharacterId);
                        break;
                    case ZoneCommandKind.GrantValleyWarRewardDrop:
                        HandleGrantValleyWarRewardDrop(command.CharacterId);
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

        if (processed >= InboxDrainCapPerTick)
            LogDrainCapEngaged(_inbox.Reader, "inbox", InboxDrainCapPerTick);
    }

    /// <summary>
    ///     Shared per-tick-drain-cap observability hook for every <c>Drain*Commands</c> method in this partial
    ///     class family (see <see cref="InboxDrainCapPerTick" />'s own remarks): logs that <paramref name="channelName" />
    ///     stopped draining at its per-tick cap with more of its own backlog still queued -- channel-specific
    ///     detail the blanket post-hoc tick-duration warning in <see cref="RunAsync" /> cannot provide on its
    ///     own (that warning fires after the fact for the WHOLE tick and never identifies which of the many
    ///     channels contributed). Nothing is dropped or lost: the remainder stays queued in the channel and is
    ///     picked up by this exact same drain call on a following tick.
    /// </summary>
    private void LogDrainCapEngaged<T>(ChannelReader<T> reader, string channelName, int cap)
    {
        var remaining = reader.CanCount ? reader.Count : -1;
        logger.LogWarning(
            "Zone {MapId} {Channel} drain hit its per-tick cap of {Cap} item(s); {Remaining} still queued and will be processed on a following tick",
            MapId, channelName, cap, remaining);
    }

    /// <summary>
    ///     Runs every registered <see cref="ISimulationSystem" /> in declared order, once per frame with a whole 500 ms
    ///     legacy tick due.
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
                logger.LogError(ex, "Zone {MapId} simulation system {System} failed", MapId,
                    system.GetType().Name);
            }
    }

    /// <summary>
    ///     Resolves <c>{GameDataDirectory}/WORLD/Z{canonicalMapId:D3}.WM</c> against the process's current working
    ///     directory (or, when <see cref="GameServerOptions.GameDataDirectory" /> is absolute, as-is), matching the
    ///     legacy <c>ServerInfo.ini</c>'s <c>DataDir=./DATA/</c> convention. The map id is first remapped to its
    ///     canonical geometry id (<see cref="ZoneCanonicalGeometryMap" />), reproducing legacy
    ///     <c>WORLD_FOR_GXD::LoadWM</c>'s dungeon/instance/war-arena mesh sharing (e.g. map 336 loads <c>Z310.WM</c>),
    ///     so those maps resolve the mesh they actually ship with instead of a non-existent per-map file.
    /// </summary>
    private static ZoneGeometry? TryLoadGeometry(short mapId, GameServerOptions gameServerOptions,
        ILogger<Zone> zoneLogger)
    {
        var canonicalMapId = ZoneCanonicalGeometryMap.ResolveCanonicalMapId(mapId);
        var wmPath = Path.Combine(Directory.GetCurrentDirectory(), gameServerOptions.GameDataDirectory, "WORLD",
            $"Z{canonicalMapId:D3}.WM");

        if (!File.Exists(wmPath))
        {
            zoneLogger.LogWarning(
                "No world geometry found at {Path} for MapId {MapId} -- monster pathing (MonsterAiSystem.MoveToward) " +
                "will not be constrained by terrain/obstacles and will not snap to ground height, and player-movement " +
                "validation (MovementRules.IsPlausible) will skip its walkability/ground-height sub-checks (the " +
                "independent per-move distance backstop still applies). Expected until real .WM navmesh assets are deployed " +
                "for this map -- see GameServerOptionsValidator's remarks for why this is not a startup-blocking check",
                wmPath, mapId);
            return null;
        }

        try
        {
            return ZoneGeometryReader.Load(wmPath);
        }
        catch (Exception ex)
        {
            zoneLogger.LogError(ex,
                "Failed to load world geometry from {Path} for MapId {MapId} -- same degraded movement consequences " +
                "as a missing file (unconstrained monster pathing, speed-only player-movement validation), but this " +
                "file exists and failed to parse/read, so unlike the missing-file case this warrants investigation",
                wmPath, mapId);
            return null;
        }
    }
}
