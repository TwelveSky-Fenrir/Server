using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
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
    TowerWarState? towerWar = null,
    WorldStateService? worldState = null,
    PartyRegistry? partyRegistry = null,
    DuelRegistry? duelRegistry = null,
    FriendRegistry? friendRegistry = null,
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
    Zone195NokSanState? zone195NokSanState = null,
    Lazy<IPartyResyncRelayQueue>? partyResyncRelayQueue = null,
    IAccountSessionRepository? accountSessions = null,
    ISessionTicketRepository? sessionTickets = null,
    CharacterPresenceOwnership? characterPresenceOwnership = null,
    ZoneCenterSiegeState? siegeState = null,
    Zone051Zone053SiegeState? zone051Zone053SiegeState = null,
    Lazy<ZoneCenterBroadcastIngestor>? siegeIngestor = null,
    Lazy<IPvpKillCooldownClaimQueue>? pvpKillCooldownClaims = null) : IZoneActor
{
    private const int InboxCapacity = 8192;

    private const int TimerWakeTaskIndex = 0;

    private const int QueueDrainLimitPerWake = 64;

    private const int DrainedQueueCount = 33;

    private static readonly TimeSpan LeaveCommandObservationTimeout = TimeSpan.FromSeconds(2);

    private readonly SimulationTickAccumulator _accumulator = new();

    private readonly IZoneClockSystem[] _clockSystems = [.. simulationSystems.OfType<IZoneClockSystem>()];

    private readonly DuelRegistry _duelRegistry = duelRegistry ?? new DuelRegistry();

    private readonly FriendRegistry _friendRegistry = friendRegistry ?? new FriendRegistry();

    private readonly AoiGrid _grid = new(options.AoiCellSize);

    private readonly HeroRankPointAccumulator _heroRankPointAccumulator =
        heroRankPointAccumulator ?? new HeroRankPointAccumulator();

    private readonly Channel<QueuedZoneCommand> _inbox = Channel.CreateBounded<QueuedZoneCommand>(
        new BoundedChannelOptions(InboxCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly KeyValuePair<string, object?> _mapTag = ZoneTickMetrics.MapTag(mapId);

    private readonly List<int> _monsterAiNeighborScratch = [];

    private readonly List<MonsterAggroCandidate> _monsterBossAggroScratch = [];

    private readonly PartyRegistry _partyRegistry = partyRegistry ?? new PartyRegistry();

    private readonly Lazy<IPartyResyncRelayQueue>? _partyResyncRelayQueue = partyResyncRelayQueue;

    private readonly PlayerRegistry _players = new();

    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Instance;

    private readonly ISessionTicketRepository? _sessionTickets = sessionTickets;

    private readonly TradeRegistry _tradeRegistry = tradeRegistry ?? new TradeRegistry();

    private readonly ZoneRegistry? _zoneRegistry = zoneRegistry;

    private TimeSpan _clock;

    private int _nextDrainQueue;

    private MonsterPathfinder? _pathfinder;

    private ImmutableArray<PlayerRuntimeState>? _simulationPlayerOrder;

    private Zone38TribeEffectSnapshot _zone38TribeEffects =
        siegeState?.CaptureZone38TribeEffects() ?? Zone38TribeEffectSnapshot.Empty;

    public short MapId { get; } = mapId;

    public int PlayerCount => _players.Count;

    public IEnumerable<PlayerRuntimeState> Players => _simulationPlayerOrder ?? _players.Values;

    public long RawLogicTick { get; private set; }

    public float AoiCellSize => options.AoiCellSize;

    public ZoneGeometry Geometry { get; } = geometry ?? LoadGeometry(mapId, options);

    public MonsterPathfinder Pathfinder => _pathfinder ??= new MonsterPathfinder(Geometry);

    public bool Post(in ZoneCommand command)
    {
        if (_inbox.Writer.TryWrite(new QueuedZoneCommand(command, Stopwatch.GetTimestamp())))
        {
            RecordCoreCommandQueueDepth();
            return true;
        }

        ZoneTickMetrics.CommandQueueRejections.Add(1, _mapTag, ZoneTickMetrics.CoreCommandQueueTag);

        if (command.Kind != ZoneCommandKind.Leave)
        {
            command.EnterSnapshot?.TrySetResult(null);
            command.ZoneTransferSnapshot?.TrySetResult(null);
            command.Completion?.TrySetResult(ZoneCommandResult.Backpressured("Zone inbox is full."));
        }

        return false;
    }

    private void RecordCoreCommandQueueDepth()
    {
        if (ZoneTickMetrics.CommandQueueDepth.Enabled && _inbox.Reader.CanCount)
            ZoneTickMetrics.CommandQueueDepth.Record(_inbox.Reader.Count, _mapTag,
                ZoneTickMetrics.CoreCommandQueueTag);
    }

    public async Task<ZoneLeaveSubmission> PostLeaveCommandAndWaitAsync(int characterId, long expectedSessionId,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        if (ct.IsCancellationRequested)
            return new ZoneLeaveSubmission(
                ZoneLeaveResult.Cancelled("Leave command was cancelled before actor submission."), null);

        var completion = new TaskCompletionSource<ZoneLeaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = ZoneCommand.Leave(characterId, expectedSessionId, completion);

        if (!Post(command)) _ = EnqueueDeferredLeaveAsync(command);

        return await ObserveLeaveAsync(completion.Task, timeout ?? LeaveCommandObservationTimeout, ct)
            .ConfigureAwait(false);
    }

    private async Task EnqueueDeferredLeaveAsync(ZoneCommand command)
    {
        try
        {
            await _inbox.Writer.WriteAsync(new QueuedZoneCommand(command, Stopwatch.GetTimestamp()))
                .ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            command.LeaveCompletion?.TrySetResult(ZoneLeaveResult.Faulted(null, ex.Message));
            logger.LogError(ex,
                "Zone {MapId} closed before it accepted the deferred Leave command for character {CharacterId}",
                MapId, command.CharacterId);
        }
    }

    private static async Task<ZoneLeaveSubmission> ObserveLeaveAsync(Task<ZoneLeaveResult> completion,
        TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            return new ZoneLeaveSubmission(await completion.WaitAsync(timeout, ct).ConfigureAwait(false), null);
        }
        catch (TimeoutException)
        {
            return new ZoneLeaveSubmission(
                ZoneLeaveResult.Unknown("Leave command observation timed out after actor admission may have occurred."),
                completion);
        }
        catch (OperationCanceledException)
        {
            return new ZoneLeaveSubmission(
                ZoneLeaveResult.Unknown(
                    "Leave command observation was cancelled after actor admission may have occurred."),
                completion);
        }
    }

    public bool TryGetPlayer(int characterId, out PlayerRuntimeState? state)
    {
        return _players.TryGetValue(characterId, out state);
    }

    public bool TryGetPlayerByName(string name, out PlayerRuntimeState? state)
    {
        foreach (var candidate in _players.Values)
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                state = candidate;
                return true;
            }

        state = null;
        return false;
    }

    public List<int> NeighborsOfPosition(float x, float z, int scale = 1)
    {
        _monsterAiNeighborScratch.Clear();
        _grid.Neighbors(_monsterAiNeighborScratch, _grid.CellOf(x, z), scale);
        return _monsterAiNeighborScratch;
    }

    public long AdvanceRawLogicTick()
    {
        return ++RawLogicTick;
    }

    public List<MonsterAggroCandidate> BorrowBossAggroScratch()
    {
        _monsterBossAggroScratch.Clear();
        return _monsterBossAggroScratch;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        var lastFrame = Stopwatch.GetTimestamp();
        var wakeTasks = CreateWakeTasks(timer, ct);

        while (true)
        {
            Task woken;

            try
            {
                woken = await Task.WhenAny(wakeTasks).ConfigureAwait(false);
                if (!await ((Task<bool>)woken).ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

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
            finally
            {
                RearmCompletedWakeTasks(wakeTasks, timer, ct);
            }
        }
    }

    private Task[] CreateWakeTasks(PeriodicTimer timer, CancellationToken ct)
    {
        return
        [
            timer.WaitForNextTickAsync(ct).AsTask(),
            WaitForQueuedWorkAsync(1, ct),
            WaitForQueuedWorkAsync(2, ct),
            WaitForQueuedWorkAsync(3, ct),
            WaitForQueuedWorkAsync(4, ct),
            WaitForQueuedWorkAsync(5, ct),
            WaitForQueuedWorkAsync(6, ct),
            WaitForQueuedWorkAsync(7, ct),
            WaitForQueuedWorkAsync(8, ct),
            WaitForQueuedWorkAsync(9, ct),
            WaitForQueuedWorkAsync(10, ct),
            WaitForQueuedWorkAsync(11, ct),
            WaitForQueuedWorkAsync(12, ct),
            WaitForQueuedWorkAsync(13, ct),
            WaitForQueuedWorkAsync(14, ct),
            WaitForQueuedWorkAsync(15, ct),
            WaitForQueuedWorkAsync(16, ct),
            WaitForQueuedWorkAsync(17, ct),
            WaitForQueuedWorkAsync(18, ct),
            WaitForQueuedWorkAsync(19, ct),
            WaitForQueuedWorkAsync(20, ct),
            WaitForQueuedWorkAsync(21, ct),
            WaitForQueuedWorkAsync(22, ct),
            WaitForQueuedWorkAsync(23, ct),
            WaitForQueuedWorkAsync(24, ct),
            WaitForQueuedWorkAsync(25, ct),
            WaitForQueuedWorkAsync(26, ct),
            WaitForQueuedWorkAsync(27, ct),
            WaitForQueuedWorkAsync(28, ct),
            WaitForQueuedWorkAsync(29, ct),
            WaitForQueuedWorkAsync(30, ct),
            WaitForQueuedWorkAsync(31, ct)
        ];
    }

    private void RearmCompletedWakeTasks(Task[] wakeTasks, PeriodicTimer timer, CancellationToken ct)
    {
        for (var index = 0; index < wakeTasks.Length; index++)
        {
            if (!wakeTasks[index].IsCompleted)
                continue;

            wakeTasks[index] = index == TimerWakeTaskIndex
                ? timer.WaitForNextTickAsync(ct).AsTask()
                : WaitForQueuedWorkAsync(index, ct);
        }
    }

    private Task<bool> WaitForQueuedWorkAsync(int wakeTaskIndex, CancellationToken ct)
    {
        return wakeTaskIndex switch
        {
            1 => _inbox.Reader.WaitToReadAsync(ct).AsTask(),
            2 => _inventoryInbox.Reader.WaitToReadAsync(ct).AsTask(),
            3 => _skillInbox.Reader.WaitToReadAsync(ct).AsTask(),
            4 => _combatInbox.Reader.WaitToReadAsync(ct).AsTask(),
            5 => _chatInbox.Reader.WaitToReadAsync(ct).AsTask(),
            6 => _mentorInbox.Reader.WaitToReadAsync(ct).AsTask(),
            7 => _questInbox.Reader.WaitToReadAsync(ct).AsTask(),
            8 => _guildInbox.Reader.WaitToReadAsync(ct).AsTask(),
            9 => _tribeInbox.Reader.WaitToReadAsync(ct).AsTask(),
            10 => _gmExperienceInbox.Reader.WaitToReadAsync(ct).AsTask(),
            11 => _gmZone124PartyPullInbox.Reader.WaitToReadAsync(ct).AsTask(),
            12 => _pshopInbox.Reader.WaitToReadAsync(ct).AsTask(),
            13 => _missionInbox.Reader.WaitToReadAsync(ct).AsTask(),
            14 => _bottleInbox.Reader.WaitToReadAsync(ct).AsTask(),
            15 => _hotkeySlotInbox.Reader.WaitToReadAsync(ct).AsTask(),
            16 => _hotkeyMoveInbox.Reader.WaitToReadAsync(ct).AsTask(),
            17 => _petBagInbox.Reader.WaitToReadAsync(ct).AsTask(),
            18 => _heroRankingInbox.Reader.WaitToReadAsync(ct).AsTask(),
            19 => _heroRankingRolloverInbox.Reader.WaitToReadAsync(ct).AsTask(),
            20 => _holyStoneCountdownEvictionInbox.Reader.WaitToReadAsync(ct).AsTask(),
            21 => _holyStoneForcedReturnInbox.Reader.WaitToReadAsync(ct).AsTask(),
            22 => _holyStoneBattleRankResetInbox.Reader.WaitToReadAsync(ct).AsTask(),
            23 => _fishingInbox.Reader.WaitToReadAsync(ct).AsTask(),
            24 => _mountInbox.Reader.WaitToReadAsync(ct).AsTask(),
            25 => _costumeInbox.Reader.WaitToReadAsync(ct).AsTask(),
            26 => _stellarCoreInbox.Reader.WaitToReadAsync(ct).AsTask(),
            27 => _avatarBuffInbox.Reader.WaitToReadAsync(ct).AsTask(),
            28 => _runeInbox.Reader.WaitToReadAsync(ct).AsTask(),
            29 => _autoBuffInbox.Reader.WaitToReadAsync(ct).AsTask(),
            30 => _guildBuffExpiryInbox.Reader.WaitToReadAsync(ct).AsTask(),
            31 => _guildBuffActivationInbox.Reader.WaitToReadAsync(ct).AsTask(),
            _ => throw new ArgumentOutOfRangeException(nameof(wakeTaskIndex), wakeTaskIndex,
                "Unknown zone command wake task.")
        };
    }

    public void Tick(TimeSpan elapsed)
    {
        _clock += elapsed;
        AdvanceClockSystems();
        var simulationDue = _accumulator.Advance(elapsed) > 0;

        var t0 = Stopwatch.GetTimestamp();
        DrainQueuedWorkRoundRobin();
        var t1 = Stopwatch.GetTimestamp();
        Simulate(simulationDue ? 1 : 0);
        var t2 = Stopwatch.GetTimestamp();

        var hasPlayers = !_players.IsEmpty;

        if (simulationDue)
        {
            try
            {
                RebroadcastAvatars();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(RebroadcastAvatars));
            }

            try
            {
                SweepStuckZoneTransfers();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                    nameof(SweepStuckZoneTransfers));
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

                try
                {
                    SummonPersonalQuestBossesForTick();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                        nameof(SummonPersonalQuestBossesForTick));
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

        if (simulationDue)
        {
            try
            {
                RebroadcastProxyShops();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId, nameof(RebroadcastProxyShops));
            }

            try
            {
                AdvanceZone241PersonalDungeonInstances(1);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} tick stage {Stage} failed", MapId,
                    nameof(AdvanceZone241PersonalDungeonInstances));
            }
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
        ZoneTickMetrics.Ticks.Add(1, _mapTag);
        ZoneTickMetrics.TickDurationMs.Record(Stopwatch.GetElapsedTime(t0, t3).TotalMilliseconds, _mapTag);

        if (elapsed > SimulationClock.LegacyTick)
        {
            ZoneTickMetrics.LateTicks.Add(1, _mapTag);
            ZoneTickMetrics.TickLatenessMs.Record((elapsed - SimulationClock.LegacyTick).TotalMilliseconds,
                _mapTag);
        }
    }

    private void AdvanceClockSystems()
    {
        foreach (var system in _clockSystems)
            try
            {
                system.AdvanceClock(this);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} clock system {System} failed", MapId, system.GetType().Name);
            }
    }

    private void DrainQueuedWorkRoundRobin()
    {
        var firstQueue = _nextDrainQueue;
        _nextDrainQueue = (firstQueue + 1) % DrainedQueueCount;

        for (var offset = 0; offset < DrainedQueueCount; offset++)
            DrainQueuedWork((firstQueue + offset) % DrainedQueueCount);
    }

    private void DrainQueuedWork(int queue)
    {
        try
        {
            switch (queue)
            {
                case 0:
                    DrainInbox(QueueDrainLimitPerWake);
                    break;
                case 1:
                    DrainInventoryCommands(QueueDrainLimitPerWake);
                    break;
                case 2:
                    DrainSkillCommands(QueueDrainLimitPerWake);
                    break;
                case 3:
                    DrainCombatCommands(QueueDrainLimitPerWake);
                    break;
                case 4:
                    DrainChatCommands(QueueDrainLimitPerWake);
                    break;
                case 5:
                    DrainMentorCommands(QueueDrainLimitPerWake);
                    break;
                case 6:
                    DrainQuestCommands(QueueDrainLimitPerWake);
                    break;
                case 7:
                    DrainGuildCommands(QueueDrainLimitPerWake);
                    break;
                case 8:
                    DrainTribeProgressCommands(QueueDrainLimitPerWake);
                    break;
                case 9:
                    DrainGmExperienceCommands(QueueDrainLimitPerWake);
                    break;
                case 10:
                    DrainGmZone124PartyPullCommands(QueueDrainLimitPerWake);
                    break;
                case 11:
                    DrainPshopCommands(QueueDrainLimitPerWake);
                    break;
                case 12:
                    DrainMissionCommands(QueueDrainLimitPerWake);
                    break;
                case 13:
                    DrainDrinkBottleCommands(QueueDrainLimitPerWake);
                    break;
                case 14:
                    DrainHotkeySlotMirrorCommands(QueueDrainLimitPerWake);
                    break;
                case 15:
                    DrainHotkeyMoveCommands(QueueDrainLimitPerWake);
                    break;
                case 16:
                    DrainPetBagCommands(QueueDrainLimitPerWake);
                    break;
                case 17:
                    DrainHeroRankingQueryCommands(QueueDrainLimitPerWake);
                    break;
                case 18:
                    DrainHeroRankingRolloverCommands(QueueDrainLimitPerWake);
                    break;
                case 19:
                    DrainHolyStoneCountdownEvictionCommands(QueueDrainLimitPerWake);
                    break;
                case 20:
                    DrainHolyStoneForcedReturnCommands(QueueDrainLimitPerWake);
                    break;
                case 21:
                    DrainHolyStoneBattleRankResetCommands(QueueDrainLimitPerWake);
                    break;
                case 22:
                    DrainFishingCommands(QueueDrainLimitPerWake);
                    break;
                case 23:
                    DrainMountCommands(QueueDrainLimitPerWake);
                    break;
                case 24:
                    DrainCostumeCommands(QueueDrainLimitPerWake);
                    break;
                case 25:
                    DrainStellarCoreCommands(QueueDrainLimitPerWake);
                    break;
                case 26:
                    DrainAvatarBuffCommands(QueueDrainLimitPerWake);
                    break;
                case 27:
                    DrainRuneSocketCommands(QueueDrainLimitPerWake);
                    break;
                case 28:
                    DrainAutoBuffCommands(QueueDrainLimitPerWake);
                    break;
                case 29:
                    DrainGuildBuffExpiryCommands(QueueDrainLimitPerWake);
                    break;
                case 30:
                    DrainGuildBuffActivationCommands(QueueDrainLimitPerWake);
                    break;
                case 31:
                    DrainClaimedGroundItemDespawns(QueueDrainLimitPerWake);
                    break;
                case 32:
                    DrainClosedProxyShopBroadcasts(QueueDrainLimitPerWake);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} queue {Queue} drain failed", MapId, queue);
        }
    }

    private void DrainInbox(int maximum)
    {
        for (var processed = 0; processed < maximum && _inbox.Reader.TryRead(out var queuedCommand); processed++)
        {
            var command = queuedCommand.Command;

            try
            {
                ZoneTickMetrics.CommandQueueAgeMs.Record(
                    Stopwatch.GetElapsedTime(queuedCommand.EnqueuedAtTimestamp).TotalMilliseconds,
                    _mapTag,
                    ZoneTickMetrics.CoreCommandQueueTag);
                switch (command.Kind)
                {
                    case ZoneCommandKind.Enter:
                        HandleEnter(command.CharacterId, command.EnterData!, command.EnterSnapshot);
                        break;
                    case ZoneCommandKind.AcknowledgeEnterBootstrap:
                        command.Completion?.TrySetResult(HandleEnterBootstrapAcknowledgement(command.CharacterId,
                            command.ExpectedSessionId));
                        break;
                    case ZoneCommandKind.Leave:
                        command.LeaveCompletion?.TrySetResult(
                            HandleLeave(command.CharacterId, command.ExpectedSessionId));
                        break;
                    case ZoneCommandKind.Move:
                        var action = command.Action;
                        HandleMove(command.CharacterId, in action, command.IsResumeAction);
                        break;
                    case ZoneCommandKind.PetAction:
                        var petAction = command.Action;
                        HandlePetAction(command.CharacterId, in petAction);
                        break;
                    case ZoneCommandKind.BeginZoneTransfer:
                    {
                        var snapshot = HandleBeginZoneTransfer(command.CharacterId, command.ZoneTransferTargetMapId,
                            command.ReviveForDeathTransfer);
                        command.ZoneTransferSnapshot?.TrySetResult(snapshot);
                        command.Completion?.TrySetResult(snapshot is null
                            ? ZoneCommandResult.Rejected("Character cannot begin a zone transfer.")
                            : ZoneCommandResult.Applied());
                        break;
                    }
                    case ZoneCommandKind.ClearZoneTransferPending:
                        command.Completion?.TrySetResult(HandleClearZoneTransferPending(command.CharacterId,
                            command.ZoneTransferRegisteredAtUtc));
                        break;
                    case ZoneCommandKind.RollbackZoneTransfer:
                        command.Completion?.TrySetResult(HandleRollbackZoneTransfer(command.CharacterId,
                            command.ZoneTransferRegisteredAtUtc));
                        break;
                    case ZoneCommandKind.RefreshZoneTransferRegistrationTimestamp:
                        command.Completion?.TrySetResult(
                            HandleRefreshZoneTransferRegistrationTimestamp(command.CharacterId));
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
                    case ZoneCommandKind.DespawnRegularWarBosses:
                        HandleDespawnRegularWarBosses();
                        command.Completion?.TrySetResult(ZoneCommandResult.Applied());
                        break;
                    case ZoneCommandKind.BroadcastDuelStart:
                        HandleBroadcastDuelStart(command.CharacterId, command.DuelOpponentCharacterId,
                            command.DuelUniqueNumber);
                        break;
                    case ZoneCommandKind.SetRegularWarSmallestTribe:
                        HandleSetRegularWarSmallestTribe(command.SmallestPresentTribe);
                        break;
                    case ZoneCommandKind.ApplyPvpKillRewardClaim:
                        HandlePvpKillCooldownClaim(command.PvpKillCooldownClaim);
                        break;
                    case ZoneCommandKind.ApplyZone38TribeEffects:
                        ApplyZone38TribeEffects(command.Zone38TribeEffects);
                        break;
                }
            }
            catch (Exception ex)
            {
                command.EnterSnapshot?.TrySetException(ex);
                command.ZoneTransferSnapshot?.TrySetException(ex);
                command.LeaveCompletion?.TrySetResult(ZoneLeaveResult.Faulted(null, ex.Message));
                command.Completion?.TrySetResult(ZoneCommandResult.Faulted(ex.Message));
                logger.LogError(ex, "Zone {MapId} command {Kind} for character {CharacterId} failed", MapId,
                    command.Kind, command.CharacterId);
            }
        }
    }

    private void Simulate(int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        _simulationPlayerOrder = _players.OrderedSnapshot;

        try
        {
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
        finally
        {
            _simulationPlayerOrder = null;
        }
    }

    internal static ZoneGeometry LoadGeometry(short mapId, GameServerOptions gameServerOptions)
    {
        var canonicalMapId = ZoneCanonicalGeometryMap.ResolveCanonicalMapId(mapId);
        var wmPath = Path.Combine(Directory.GetCurrentDirectory(), gameServerOptions.GameDataDirectory, "WORLD",
            $"Z{canonicalMapId:D3}.WM");

        if (!File.Exists(wmPath))
            throw new FileNotFoundException($"Required world geometry for map {mapId} was not found.", wmPath);

        try
        {
            return ZoneGeometryReader.Load(wmPath);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Required world geometry for map {mapId} is invalid: {wmPath}", ex);
        }
    }

    private readonly record struct QueuedZoneCommand(ZoneCommand Command, long EnqueuedAtTimestamp);
}
