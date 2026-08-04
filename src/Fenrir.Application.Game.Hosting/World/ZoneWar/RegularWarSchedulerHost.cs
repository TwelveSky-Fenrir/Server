using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class RegularWarSchedulerHost(
    ZoneRegistry zones,
    WorldStateService worldState,
    IRegularWarEventSink sink,
    IRegularWarRewardValueProvider rewardValues,
    ILogger<RegularWarSchedulerHost> logger,
    IWorldEventSnapshotRepository snapshots,
    RegularWarActiveMapTracker? activeMapTracker = null) : BackgroundService
{
    private const string SnapshotEventKind = "regular-war-schedule-v1";
    private const string SnapshotOccurrencePrefix = "map:";
    private const int SnapshotFlushIntervalTicks = 10;

    private readonly SimulationTickAccumulator _accumulator = new();

    private readonly RegularWarActiveMapTracker
        _activeMapTracker = activeMapTracker ?? new RegularWarActiveMapTracker();

    private readonly Dictionary<short, RegularWarSchedule> _schedules = new();

    private readonly Dictionary<short, long> _snapshotRevisionByMapId = new();

    private readonly HashSet<short> _dirtyMapIds = [];

    private readonly HashSet<short> _blockedMapIds = [];

    private bool _initialized;

    private int _ticksSinceSnapshotFlush;

    public bool IsInitialized => _initialized;

    public void Tick(TimeSpan elapsed)
    {
        if (!_initialized)
            return;

        var wholeTicks = _accumulator.Advance(elapsed);
        for (var i = 0; i < wholeTicks; i++)
            Dispatch(AdvanceOneLegacyTick());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await InitializeAsync(stoppingToken).ConfigureAwait(false))
            return;

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    await AdvanceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Regular War scheduler tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await FlushDirtySnapshotsAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask<bool> InitializeAsync(CancellationToken ct)
    {
        if (_initialized)
            return true;

        IReadOnlyCollection<WorldEventSnapshotRowDto> rows;
        try
        {
            rows = await snapshots.LoadAllAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Regular War scheduler will not start because durable snapshots could not load");
            return false;
        }

        foreach (var row in rows)
        {
            if (!string.Equals(row.EventKind, SnapshotEventKind, StringComparison.Ordinal))
                continue;

            if (!TryParseMapId(row.OccurrenceKey, out var mapId) ||
                !RegularWarMapCatalog.TryGet(mapId, out var mapConfig) || !zones.TryGet(mapId, out _))
                continue;

            try
            {
                var state = DeserializeState(row);
                var schedule = new RegularWarSchedule(mapConfig);
                schedule.RestoreState(state);

                _schedules.Add(mapId, schedule);
                _snapshotRevisionByMapId.Add(mapId, row.Revision);
                _activeMapTracker.ReportPhase(mapId, schedule.Phase);
                _activeMapTracker.ReportWarCycle(mapId, schedule.WarCycleNumber);

                if (schedule.Phase is RegularWarPhase.PostWarCleanup or RegularWarPhase.ForcedReset)
                {
                    _blockedMapIds.Add(mapId);
                    logger.LogCritical(
                        "Regular War map {MapId} remains fail-closed after restoring phase {Phase} because its reward or teardown effect is not independently idempotent",
                        mapId, schedule.Phase);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _blockedMapIds.Add(mapId);
                logger.LogCritical(ex,
                    "Regular War map {MapId} remains fail-closed because its durable snapshot is invalid", mapId);
            }
        }

        _initialized = true;
        return true;
    }

    private async ValueTask AdvanceAsync(CancellationToken ct)
    {
        var wholeTicks = _accumulator.Advance(SimulationClock.LegacyTick);
        for (var i = 0; i < wholeTicks; i++)
        {
            var workItems = AdvanceOneLegacyTick();
            foreach (var workItem in workItems)
            {
                var requiresSnapshot = workItem.Result.Phase != workItem.Result.PreviousPhase ||
                                       workItem.Result.AllSessionsShouldDisconnect;
                if (requiresSnapshot && !await PersistMapSnapshotAsync(workItem.MapConfig.MapId, ct).ConfigureAwait(false))
                {
                    _blockedMapIds.Add(workItem.MapConfig.MapId);
                    logger.LogCritical(
                        "Regular War map {MapId} remains fail-closed because phase {Phase} could not be durably recorded before effects",
                        workItem.MapConfig.MapId, workItem.Result.Phase);
                    continue;
                }

                Dispatch(workItem);
            }

            _ticksSinceSnapshotFlush++;
            if (_ticksSinceSnapshotFlush >= SnapshotFlushIntervalTicks)
            {
                _ticksSinceSnapshotFlush = 0;
                await FlushDirtySnapshotsAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private List<RegularWarTickWorkItem> AdvanceOneLegacyTick()
    {
        var workItems = new List<RegularWarTickWorkItem>();
        var worldSnapshot = worldState.World;
        if (worldSnapshot.TribeSymbolBattle)
        {
            PauseForTribeSymbolBattle();
            return workItems;
        }

        foreach (var mapConfig in RegularWarMapCatalog.ConfiguredMaps)
        {
            if (!zones.TryGet(mapConfig.MapId, out var zone))
                continue;

            if (_blockedMapIds.Contains(mapConfig.MapId))
                continue;

            var schedule = GetOrCreateSchedule(mapConfig);
            var snapshot = BuildSnapshot(zone);

            _activeMapTracker.DrainPendingKills(mapConfig.MapId, schedule);

            var result = schedule.Tick(snapshot);
            _activeMapTracker.ReportPhase(mapConfig.MapId, result.Phase);
            _activeMapTracker.ReportWarCycle(mapConfig.MapId, schedule.WarCycleNumber);

            _dirtyMapIds.Add(mapConfig.MapId);
            workItems.Add(new RegularWarTickWorkItem(mapConfig, zone, schedule, result));
        }

        return workItems;
    }

    private void Dispatch(IEnumerable<RegularWarTickWorkItem> workItems)
    {
        foreach (var workItem in workItems)
            Dispatch(workItem);
    }

    private void Dispatch(in RegularWarTickWorkItem workItem)
    {
        var mapConfig = workItem.MapConfig;
        var zone = workItem.Zone;
        var schedule = workItem.Schedule;
        var result = workItem.Result;

        if (result.CountdownAnnounceValue is { } remaining)
            sink.OnCountdownAnnounced(mapConfig.MapId, remaining);

        if (result.CountdownFinished)
            sink.OnCountdownFinished(mapConfig.MapId);

        if (result.GateOpened)
            sink.OnGateOpened(mapConfig.MapId);

        if (result.SmallestPresentTribe is { } smallestTribe)
            sink.OnSmallestTribeFlagged(mapConfig.MapId, smallestTribe);

        if (result.EnteredActiveWar)
            sink.OnActiveWarStarted(mapConfig.MapId,
                result.ActiveWarDurationTicks ?? RegularWarSchedule.ActiveWarDurationTicks);

        if (result.Outcome is { } outcome)
            HandleConclusion(mapConfig, zone, schedule, outcome, result.WinningTribe,
                result.BossMonstersShouldSpawn);

        if (result.ReturnToTownAnnounced)
            sink.OnReturnToTownAnnounced(mapConfig.MapId);

        if (result.MonstersShouldDespawn)
            sink.OnMonstersShouldDespawn(mapConfig.MapId);

        if (result.AllSessionsShouldDisconnect)
            sink.OnAllSessionsShouldDisconnect(mapConfig.MapId);
    }

    private async ValueTask FlushDirtySnapshotsAsync(CancellationToken ct)
    {
        foreach (var mapId in _dirtyMapIds.OrderBy(static mapId => mapId).ToArray())
            if (!await PersistMapSnapshotAsync(mapId, ct).ConfigureAwait(false))
                logger.LogError("Regular War map {MapId}: periodic durable snapshot flush failed", mapId);
    }

    private async ValueTask<bool> PersistMapSnapshotAsync(short mapId, CancellationToken ct)
    {
        if (!_schedules.TryGetValue(mapId, out var schedule))
            return true;

        var state = schedule.CaptureState();
        var payload = JsonSerializer.Serialize(state,
            RegularWarScheduleJsonContext.Default.RegularWarScheduleState);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var expectedRevision = _snapshotRevisionByMapId.GetValueOrDefault(mapId);

        try
        {
            if (!await snapshots.TryApplyAsync(SnapshotEventKind, FormatOccurrenceKey(mapId), expectedRevision,
                    state.Phase.ToString(), payload, hash, ct).ConfigureAwait(false))
            {
                logger.LogError(
                    "Regular War map {MapId}: durable snapshot CAS conflicted at revision {ExpectedRevision}",
                    mapId, expectedRevision);
                return false;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Regular War map {MapId}: durable snapshot write failed", mapId);
            return false;
        }

        _snapshotRevisionByMapId[mapId] = expectedRevision + 1;
        _dirtyMapIds.Remove(mapId);
        return true;
    }

    private static RegularWarScheduleState DeserializeState(WorldEventSnapshotRowDto row)
    {
        if (row.CanonicalPayloadHash is not { Length: SHA256.HashSizeInBytes } payloadHash ||
            !CryptographicOperations.FixedTimeEquals(payloadHash,
                SHA256.HashData(Encoding.UTF8.GetBytes(row.CanonicalPayload))))
            throw new InvalidOperationException("Regular War durable snapshot hash is invalid.");

        var state = JsonSerializer.Deserialize(row.CanonicalPayload,
            RegularWarScheduleJsonContext.Default.RegularWarScheduleState);
        if (!string.Equals(row.Phase, state.Phase.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException("Regular War durable snapshot phase disagrees with its payload.");

        return state;
    }

    private static string FormatOccurrenceKey(short mapId) =>
        SnapshotOccurrencePrefix + mapId.ToString(CultureInfo.InvariantCulture);

    private static bool TryParseMapId(string occurrenceKey, out short mapId)
    {
        mapId = default;
        return occurrenceKey.StartsWith(SnapshotOccurrencePrefix, StringComparison.Ordinal) &&
               short.TryParse(occurrenceKey.AsSpan(SnapshotOccurrencePrefix.Length), NumberStyles.None,
                   CultureInfo.InvariantCulture, out mapId);
    }

    private void PauseForTribeSymbolBattle()
    {
        foreach (var mapConfig in RegularWarMapCatalog.ConfiguredMaps)
        {
            if (!zones.TryGet(mapConfig.MapId, out _))
                continue;

            if (!_schedules.TryGetValue(mapConfig.MapId, out var schedule))
                continue;

            _activeMapTracker.ReportPhase(mapConfig.MapId, RegularWarPhase.Idle);
            _activeMapTracker.ReportWarCycle(mapConfig.MapId, schedule.WarCycleNumber);
            _activeMapTracker.ClearPendingKills(mapConfig.MapId);
        }
    }

    private void HandleConclusion(RegularWarMapConfig mapConfig, Zone zone, RegularWarSchedule schedule,
        RegularWarOutcome outcome, byte? winningTribe, bool bossMonstersShouldSpawn)
    {
        if (outcome == RegularWarOutcome.AbortedEmptyMap)
        {
            sink.OnWarConcluded(mapConfig.MapId, outcome, winningTribe, [], false);
            return;
        }

        var allyOfWinningTribe = winningTribe is { } winner ? worldState.GetAllyOf(winner) : null;
        var participants = BuildParticipants(zone);
        var topKillers = schedule.GetTopKillers();

        var rewards = RegularWarRewardCalculator.Compute(outcome, winningTribe, allyOfWinningTribe, mapConfig,
            participants, topKillers, rewardValues);

        CreditDailyMissionAndWaterfallQuest(zone);

        sink.OnWarConcluded(mapConfig.MapId, outcome, winningTribe, rewards, bossMonstersShouldSpawn);
    }

    private void CreditDailyMissionAndWaterfallQuest(Zone zone)
    {
        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            if (!zone.Post(ZoneCommand.CreditRegularWarConclusion(player.CharacterId)))
                logger.LogWarning(
                    "Zone {MapId} inbox full: dropped RegularWar conclusion credit for character {CharacterId}",
                    zone.MapId, player.CharacterId);
        }
    }

    private RegularWarSchedule GetOrCreateSchedule(RegularWarMapConfig mapConfig)
    {
        if (_schedules.TryGetValue(mapConfig.MapId, out var existing))
            return existing;

        var created = new RegularWarSchedule(mapConfig);
        _schedules[mapConfig.MapId] = created;
        _dirtyMapIds.Add(mapConfig.MapId);
        return created;
    }

    private static RegularWarEnvironmentSnapshot BuildSnapshot(Zone zone)
    {
        var totalPresent = 0;
        var perTribe = new int[RegularWarSchedule.TribeCount];

        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone || player.VisibleState == 0)
                continue;

            totalPresent++;
            if (player.Tribe < RegularWarSchedule.TribeCount)
                perTribe[player.Tribe]++;
        }

        return new RegularWarEnvironmentSnapshot(totalPresent, [.. perTribe]);
    }

    private static List<RegularWarParticipant> BuildParticipants(Zone zone)
    {
        var participants = new List<RegularWarParticipant>();
        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            participants.Add(new RegularWarParticipant(player.CharacterId, player.Tribe, player.Level,
                player.Level2, player.RebirthCount));
        }

        return participants;
    }

    private readonly record struct RegularWarTickWorkItem(
        RegularWarMapConfig MapConfig,
        Zone Zone,
        RegularWarSchedule Schedule,
        RegularWarTickResult Result);
}
