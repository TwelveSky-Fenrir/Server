using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarSchedulerHostTests
{
    /// <summary>Total legacy ticks from a fresh schedule up to (and including) the tick Active war begins.</summary>
    private static int TicksToActiveWar =>
        RegularWarSchedule.CooldownTicks +
        RegularWarSchedule.CountdownAnnounceStartValue * RegularWarSchedule.CountdownAnnounceIntervalTicks +
        RegularWarSchedule.FinalWaitTicks +
        RegularWarSchedule.OpenGateTicks +
        RegularWarSchedule.PreWarTicks;

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(),
            NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void UnconfiguredMap_IsNeverScheduled_NoEventsFire()
    {
        var registry = CreateRegistry(1); // map 1 is not one of the 11 configured Regular War maps
        var sink = new RecordingSink();
        var host = new RegularWarSchedulerHost(registry, CreateWorldState(), sink,
            new UnavailableRegularWarRewardValueProvider());

        host.Tick(SimulationClock.LegacyTick * (TicksToActiveWar + 100));

        Assert.Empty(sink.ActiveWarStarts);
        Assert.Empty(sink.Conclusions);
    }

    [Fact]
    public void ConfiguredHostedMap_ReachesActiveWar_AfterTheFullRampUp()
    {
        var registry = CreateRegistry(49); // one of the 11 configured maps
        var sink = new RecordingSink();
        var host = new RegularWarSchedulerHost(registry, CreateWorldState(), sink,
            new UnavailableRegularWarRewardValueProvider());

        host.Tick(SimulationClock.LegacyTick * TicksToActiveWar);

        Assert.Equal([49], sink.ActiveWarStarts);
        Assert.NotEmpty(sink.CountdownAnnouncements);
        Assert.All(sink.CountdownAnnouncements, a => Assert.Equal(49, a.MapId));
    }

    [Fact]
    public void ConfiguredHostedMap_ConcludesByElimination_AndComputesRewardsFromLivePlayers()
    {
        var registry = CreateRegistry(49);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);
        registry[49].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 49, tribe: 0)));
        registry[49].Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(sessionB, 49, tribe: 0)));
        registry[49].Tick(TimeSpan.FromMilliseconds(50));

        var sink = new RecordingSink();
        var host = new RegularWarSchedulerHost(registry, CreateWorldState(), sink,
            new UnavailableRegularWarRewardValueProvider());

        // Reach Active (only tribe 0 present the whole time, but elimination is only evaluated once Active
        // starts, so this alone doesn't conclude the war early).
        host.Tick(SimulationClock.LegacyTick * TicksToActiveWar);
        Assert.Single(sink.ActiveWarStarts);

        // One more evaluation cadence: still only tribe 0 present -> immediate elimination win.
        host.Tick(SimulationClock.LegacyTick * RegularWarSchedule.ActiveEvaluationCadenceTicks);

        var conclusion = Assert.Single(sink.Conclusions);
        Assert.Equal((short)49, conclusion.MapId);
        Assert.Equal(RegularWarOutcome.TribeWin, conclusion.Outcome);
        Assert.Equal((byte)0, conclusion.WinningTribe);
        Assert.Equal(2, conclusion.Rewards.Length);
        Assert.All(conclusion.Rewards, r => Assert.True(r.IsWinningSide));
    }

    private sealed class RecordingSink : IRegularWarEventSink
    {
        public readonly List<short> ActiveWarStarts = [];

        public readonly List<(short MapId, RegularWarOutcome Outcome, byte? WinningTribe,
            ImmutableArray<RegularWarRewardGrant> Rewards, bool BossSpawn)> Conclusions = [];

        public readonly List<(short MapId, int RemainingMinutes)> CountdownAnnouncements = [];

        public void OnCountdownAnnounced(short mapId, int remainingMinutes)
        {
            CountdownAnnouncements.Add((mapId, remainingMinutes));
        }

        public void OnSmallestTribeFlagged(short mapId, byte tribeId)
        {
        }

        public void OnActiveWarStarted(short mapId)
        {
            ActiveWarStarts.Add(mapId);
        }

        public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
            ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
        {
            Conclusions.Add((mapId, outcome, winningTribe, rewards, bossMonstersShouldSpawn));
        }

        public void OnMonstersShouldDespawn(short mapId)
        {
        }

        public void OnAllSessionsShouldDisconnect(short mapId)
        {
        }
    }
}
