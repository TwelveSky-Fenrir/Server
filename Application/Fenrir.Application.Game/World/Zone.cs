using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World.Geometry;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     One zone actor per hosted map. Every player position, the AOI grid, and the dirty-tracker marking are
///     touched only from this zone's tick (<see cref="RunAsync" /> -&gt; <see cref="Tick" />) -- everything else
///     only ever calls <see cref="Post" /> and waits for the next tick.
/// </summary>
/// <remarks>
///     The tick runs in stages: drain inbox -&gt; simulate (whole 500 ms legacy ticks via
///     <see cref="SimulationTickAccumulator" />) -&gt; periodic keep-alive rebroadcast (avatars every 3.5 s,
///     monsters/items every 5 s, see <see cref="SimulationClock" />).
/// </remarks>
/// <remarks>
///     This class is split across several partial files by concern, all in this same folder:
///     <c>Zone.Monsters.cs</c> (monster spawn/damage/death + monster-kill money grants),
///     <c>Zone.GroundItems.cs</c> (ground-item spawn/claim/expiry), <c>Zone.Combat.cs</c> (attack resolution +
///     kill hooks), <c>Zone.Chat.cs</c> (local/shout/tribe chat), <c>Zone.PlayerLifecycle.cs</c>
///     (enter/leave/move/death/revive/skill-cast + avatar-action broadcast), <c>Zone.EconomyMirrors.cs</c>
///     (already-SQL-durable inventory/skill/mentor/guild/tribe/quest/mission mirrors), and
///     <c>Zone.CosmeticMirrors.cs</c> (drink-bottle/hero-ranking/fishing/mount/costume/stellar-core/avatar-buff/
///     rune-socket/auto-buff/pshop mirrors). This file keeps the constructor, the fields/state shared across
///     several of those concerns (the AOI grid, the player map, the tick clock, RNG), and the tick loop itself.
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
    KillCooldownTracker? killCooldownTracker = null) : IZoneActor
{
    private readonly SimulationTickAccumulator _accumulator = new();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(8192) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    // ConcurrentDictionary, not a plain Dictionary: the tick is the sole writer, but the write-behind flush
    // callback and the directory-heartbeat CCU count both read this from other threads.
    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

    /// <summary>Combat/skill RNG -- <see cref="SystemRandomSource" /> in production, injectable for deterministic tests.</summary>
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

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
    /// </summary>
    public ZoneGeometry? Geometry { get; } = TryLoadGeometry(mapId, options, logger);

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

    /// <summary>Tick-thread-only: <see cref="_grid" /> itself is not thread-safe.</summary>
    internal IEnumerable<int> NeighborsOfPosition(float x, float z)
    {
        return _grid.Neighbors(_grid.CellOf(x, z));
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

            Tick(elapsed);

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
        DrainPshopCommands();
        DrainMissionCommands();
        DrainDrinkBottleCommands();
        DrainHeroRankingQueryCommands();
        DrainFishingCommands();
        DrainMountCommands();
        DrainCostumeCommands();
        DrainStellarCoreCommands();
        DrainAvatarBuffCommands();
        DrainRuneSocketCommands();
        DrainAutoBuffCommands();
        var t1 = Stopwatch.GetTimestamp();
        Simulate(_accumulator.Advance(elapsed));
        var t2 = Stopwatch.GetTimestamp();
        ProcessPendingRevives();
        RebroadcastAvatars();
        // Claimed-item despawns first (so other players stop seeing it ASAP), then the 5 s keep-alives, then
        // the 60 s expiry sweep.
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
                    case ZoneCommandKind.PetAction:
                        var petAction = command.Action;
                        HandlePetAction(command.CharacterId, in petAction);
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
    ///     Resolves <c>{GameDataDirectory}/WORLD/Z{mapId:D3}.WM</c> against the process's current working
    ///     directory, matching the legacy <c>ServerInfo.ini</c>'s <c>DataDir=./DATA/</c> convention.
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
