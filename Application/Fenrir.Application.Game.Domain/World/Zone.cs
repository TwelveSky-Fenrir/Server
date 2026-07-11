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
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

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
    ZoneGeometry? geometry = null,
    IEventLogQueue? eventLogQueue = null,
    IFourGuildKillPointQueue? fourGuildKillPointQueue = null,
    TribeSymbolCombatModifiers? tribeSymbolCombatModifiers = null,
    IPartyResyncRelayQueue? partyResyncRelayQueue = null) : IZoneActor
{

        private const int InboxCapacity = 8192;

        private const int InboxDrainCapPerTick = InboxCapacity / 2;

    private readonly SimulationTickAccumulator _accumulator = new();

        private readonly DuelRegistry _duelRegistry = duelRegistry ?? new DuelRegistry();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

        private readonly HeroRankPointAccumulator _heroRankPointAccumulator =
        heroRankPointAccumulator ?? new HeroRankPointAccumulator();

    private readonly Channel<ZoneCommand> _inbox = Channel.CreateBounded<ZoneCommand>(
        new BoundedChannelOptions(InboxCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

        private readonly List<int> _monsterAiNeighborScratch = [];

        private readonly List<MonsterAggroCandidate> _monsterBossAggroScratch = [];

        private readonly PartyRegistry _partyRegistry = partyRegistry ?? new PartyRegistry();

        private readonly IPartyResyncRelayQueue? _partyResyncRelayQueue = partyResyncRelayQueue;

    private readonly ConcurrentDictionary<int, PlayerRuntimeState> _players = new();

        private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

        private readonly TradeRegistry _tradeRegistry = tradeRegistry ?? new TradeRegistry();

        private readonly ZoneRegistry? _zoneRegistry = zoneRegistry;

        private TimeSpan _clock;

        private MonsterPathfinder? _pathfinder;

        public short MapId { get; } = mapId;

    public int PlayerCount => _players.Count;

        public IEnumerable<PlayerRuntimeState> Players => _players.Values;

        public ZoneGeometry? Geometry { get; } = geometry ?? TryLoadGeometry(mapId, options, logger);

        public MonsterPathfinder? Pathfinder =>
        Geometry is null
            ? null
            : _pathfinder ??= new MonsterPathfinder(Geometry, options.MonsterPathfindingBudgetPerTick);

        public void ResetPathBudget()
    {
        _pathfinder?.ResetBudget();
    }

        public bool Post(in ZoneCommand command)
    {
        return _inbox.Writer.TryWrite(command);
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

        public List<int> NeighborsOfPosition(float x, float z)
    {
        _monsterAiNeighborScratch.Clear();
        _grid.Neighbors(_monsterAiNeighborScratch, _grid.CellOf(x, z));
        return _monsterAiNeighborScratch;
    }

        public List<MonsterAggroCandidate> BorrowBossAggroScratch()
    {
        _monsterBossAggroScratch.Clear();
        return _monsterBossAggroScratch;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromMilliseconds(1000.0 / options.TickRateHz);
        using var timer = new PeriodicTimer(tickInterval);

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
                logger.LogError(ex, "Zone {MapId} tick failed; skipping the remainder of this tick", MapId);
            }

            var tickMs = Stopwatch.GetElapsedTime(now).TotalMilliseconds;
            if (tickMs > tickInterval.TotalMilliseconds)
                logger.LogWarning("Zone {MapId} tick took {ElapsedMs:F1} ms (budget {BudgetMs:F1} ms)", MapId,
                    tickMs, tickInterval.TotalMilliseconds);
        }
    }

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
        DrainHotkeyMoveCommands();
        DrainPetBagCommands();
        DrainHeroRankingQueryCommands();
        DrainHeroRankingRolloverCommands();
        DrainHolyStoneBattleRankResetCommands();
        DrainFishingCommands();
        DrainMountCommands();
        DrainCostumeCommands();
        DrainStellarCoreCommands();
        DrainAvatarBuffCommands();
        DrainRuneSocketCommands();
        DrainAutoBuffCommands();
        DrainGuildBuffExpiryCommands();
        DrainGuildBuffActivationCommands();
        var t1 = Stopwatch.GetTimestamp();
        var legacyTicksElapsed = _accumulator.Advance(elapsed);
        Simulate(legacyTicksElapsed);
        var t2 = Stopwatch.GetTimestamp();

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
                    case ZoneCommandKind.CreditZone038Occupation:
                        HandleZone038OccupationCredit(command.CharacterId, command.WinningTribe);
                        break;
                    case ZoneCommandKind.ApplyRegularWarReward:
                        HandleApplyRegularWarReward(command.RegularWarReward);
                        break;
                    case ZoneCommandKind.SummonRegularWarBoss:
                        HandleSummonRegularWarBoss();
                        break;
                    case ZoneCommandKind.BroadcastDuelStart:
                        HandleBroadcastDuelStart(command.CharacterId, command.DuelOpponentCharacterId,
                            command.DuelUniqueNumber);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} command {Kind} for character {CharacterId} failed", MapId,
                    command.Kind, command.CharacterId);
            }
        }

        if (processed >= InboxDrainCapPerTick)
            LogDrainCapEngaged(_inbox.Reader, "inbox", InboxDrainCapPerTick);
    }

        private void LogDrainCapEngaged<T>(ChannelReader<T> reader, string channelName, int cap)
    {
        var remaining = reader.CanCount ? reader.Count : -1;
        logger.LogWarning(
            "Zone {MapId} {Channel} drain hit its per-tick cap of {Cap} item(s); {Remaining} still queued and will be processed on a following tick",
            MapId, channelName, cap, remaining);
    }

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
