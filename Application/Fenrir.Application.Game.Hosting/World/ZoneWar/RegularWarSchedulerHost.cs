using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class RegularWarSchedulerHost(
    ZoneRegistry zones,
    WorldStateService worldState,
    IRegularWarEventSink sink,
    IRegularWarRewardValueProvider rewardValues,
    ILogger<RegularWarSchedulerHost> logger,
    RegularWarActiveMapTracker? activeMapTracker = null) : BackgroundService
{
    private readonly SimulationTickAccumulator _accumulator = new();

    private readonly RegularWarActiveMapTracker
        _activeMapTracker = activeMapTracker ?? new RegularWarActiveMapTracker();

    private readonly Dictionary<short, RegularWarSchedule> _schedules = new();

    public void Tick(TimeSpan elapsed)
    {
        var wholeTicks = _accumulator.Advance(elapsed);
        for (var i = 0; i < wholeTicks; i++)
            AdvanceOneLegacyTick();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    Tick(SimulationClock.LegacyTick);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Regular War scheduler tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AdvanceOneLegacyTick()
    {
        foreach (var mapConfig in RegularWarMapCatalog.ConfiguredMaps)
        {
            if (!zones.TryGet(mapConfig.MapId, out var zone))
                continue;

            var schedule = GetOrCreateSchedule(mapConfig);
            var snapshot = BuildSnapshot(zone);

            var result = schedule.Tick(snapshot);
            _activeMapTracker.ReportPhase(mapConfig.MapId, result.Phase);

            if (result.CountdownAnnounceValue is { } remaining)
                sink.OnCountdownAnnounced(mapConfig.MapId, remaining);

            if (result.SmallestPresentTribe is { } smallestTribe)
                sink.OnSmallestTribeFlagged(mapConfig.MapId, smallestTribe);

            if (result.EnteredActiveWar)
                sink.OnActiveWarStarted(mapConfig.MapId);

            if (result.Outcome is { } outcome)
                HandleConclusion(mapConfig, zone, schedule, outcome, result.WinningTribe,
                    result.BossMonstersShouldSpawn);

            if (result.MonstersShouldDespawn)
                sink.OnMonstersShouldDespawn(mapConfig.MapId);

            if (result.AllSessionsShouldDisconnect)
                sink.OnAllSessionsShouldDisconnect(mapConfig.MapId);
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

    private static void CreditDailyMissionAndWaterfallQuest(Zone zone)
    {
        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            zone.Post(ZoneCommand.CreditRegularWarConclusion(player.CharacterId));
        }
    }

    private RegularWarSchedule GetOrCreateSchedule(RegularWarMapConfig mapConfig)
    {
        if (_schedules.TryGetValue(mapConfig.MapId, out var existing))
            return existing;

        var created = new RegularWarSchedule(mapConfig);
        _schedules[mapConfig.MapId] = created;
        return created;
    }

    private static RegularWarEnvironmentSnapshot BuildSnapshot(Zone zone)
    {
        var totalPresent = 0;
        var perTribe = new int[RegularWarSchedule.TribeCount];

        foreach (var player in zone.Players)
        {
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
            participants.Add(new RegularWarParticipant(player.CharacterId, player.Tribe, player.Level,
                player.Level2, player.RebirthCount));

        return participants;
    }
}
