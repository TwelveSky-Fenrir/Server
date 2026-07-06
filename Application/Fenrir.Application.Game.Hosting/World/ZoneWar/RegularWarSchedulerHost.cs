using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Drives one <see cref="RegularWarSchedule" /> per configured Regular War (Zone049) map that this shard
///     actually hosts, at the legacy 2 Hz tick cadence -- the Hosting-level "scheduler" half of this cluster;
///     <see cref="RegularWarSchedule" /> itself is pure and knows nothing about <see cref="Zone" /> or timers.
/// </summary>
/// <remarks>
///     <para>
///         Gathers each map's per-tick <see cref="RegularWarEnvironmentSnapshot" /> from live
///         <see cref="PlayerRuntimeState" /> reads (tribe/present-count only) -- the exact same
///         read-only-cross-thread posture <c>ZoneWarTickService.ComputeLiveTribeHeadcount</c> already uses for
///         this same map family, not a new pattern. <see cref="RegularWarEnvironmentSnapshot.BossMonsterAlive" />
///         is always reported <see langword="false" /> (see the constructor remarks) -- Fenrir has no monster
///         catalog entry identified yet for the boss-war (server 295) post-war spawn, so the boss-war post-war
///         poll in <see cref="RegularWarSchedule" /> degrades to a flat
///         <see cref="RegularWarSchedule.BossConfirmedAbsenceTicks" /> wait instead of genuinely polling
///         liveness; documented gap, not a silent one.
///     </para>
///     <para>
///         Every actual mutation (reward grants, disconnects, monster spawn/despawn) is delegated to
///         <see cref="IRegularWarEventSink" />, defaulting to <see cref="LoggingRegularWarEventSink" /> -- see
///         that class's own remarks for why this host is safe to run continuously in production today even
///         though no map has ever been observed to reach <see cref="RegularWarPhase.ForcedReset" /> against a
///         real player yet.
///     </para>
///     <para>
///         Also reports each hosted map's freshly-ticked <see cref="RegularWarSchedule.Phase" /> to
///         <see cref="RegularWarActiveMapTracker" /> every tick -- the Domain-owned, Hosting-updated shared
///         state <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s PvP kill-reward pipeline reads back
///         to gate the Regular-War-host flat CP/War Point/Blood Point override. That tracker's own remarks
///         explain why a small Domain-owned class, rather than this Hosting class directly, is the bridge
///         Zone (Domain) can share without inverting the Domain -&gt; Hosting dependency direction.
///     </para>
/// </remarks>
public sealed class RegularWarSchedulerHost(
    ZoneRegistry zones,
    WorldStateService worldState,
    IRegularWarEventSink sink,
    IRegularWarRewardValueProvider rewardValues,
    RegularWarActiveMapTracker? activeMapTracker = null) : BackgroundService
{
    private readonly RegularWarActiveMapTracker _activeMapTracker = activeMapTracker ?? new RegularWarActiveMapTracker();
    private readonly SimulationTickAccumulator _accumulator = new();
    private readonly Dictionary<short, RegularWarSchedule> _schedules = new();

    /// <summary>
    ///     Advances every hosted configured map's schedule by <paramref name="elapsed" /> real time -- pure and
    ///     timer-free so tests can drive it directly, same convention as
    ///     <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.ZoneWarTickService.Tick" />.
    /// </summary>
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
                Tick(SimulationClock.LegacyTick);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private void AdvanceOneLegacyTick()
    {
        foreach (var mapConfig in RegularWarMapCatalog.ConfiguredMaps)
        {
            if (!zones.TryGet(mapConfig.MapId, out var zone))
                continue; // not hosted by this shard

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

        sink.OnWarConcluded(mapConfig.MapId, outcome, winningTribe, rewards, bossMonstersShouldSpawn);
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
