using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class CompositeRegularWarEventSinkTests
{
    [Fact]
    public void OnWarConcluded_FansOutToEverySink_InOrder()
    {
        var first = new SpySink();
        var second = new SpySink();
        var composite = new CompositeRegularWarEventSink([first, second],
            NullLogger<CompositeRegularWarEventSink>.Instance);

        composite.OnWarConcluded(49, RegularWarOutcome.TribeWin, 0, [], false);

        Assert.Equal(1, first.WarConcludedCalls);
        Assert.Equal(1, second.WarConcludedCalls);
    }

    [Fact]
    public void OneSinkThrows_TheOtherSinkStillReceivesTheEvent()
    {
        var broken = new SpySink { ThrowOnWarConcluded = true };
        var healthy = new SpySink();
        var composite = new CompositeRegularWarEventSink([broken, healthy],
            NullLogger<CompositeRegularWarEventSink>.Instance);

        composite.OnWarConcluded(49, RegularWarOutcome.Draw, null, [], false);

        Assert.Equal(1, broken.WarConcludedCalls);
        Assert.Equal(1, healthy.WarConcludedCalls);
    }

    [Fact]
    public void EveryOtherLifecycleEvent_FansOutToo()
    {
        var sink = new SpySink();
        var composite = new CompositeRegularWarEventSink([sink], NullLogger<CompositeRegularWarEventSink>.Instance);

        composite.OnCountdownAnnounced(49, 5);
        composite.OnSmallestTribeFlagged(49, 2);
        composite.OnActiveWarStarted(49);
        composite.OnMonstersShouldDespawn(49);
        composite.OnAllSessionsShouldDisconnect(49);

        Assert.Equal(1, sink.CountdownCalls);
        Assert.Equal(1, sink.SmallestTribeCalls);
        Assert.Equal(1, sink.ActiveWarStartedCalls);
        Assert.Equal(1, sink.MonstersShouldDespawnCalls);
        Assert.Equal(1, sink.AllSessionsShouldDisconnectCalls);
    }

    private sealed class SpySink : IRegularWarEventSink
    {
        public bool ThrowOnWarConcluded { get; init; }
        public int ActiveWarStartedCalls { get; private set; }
        public int AllSessionsShouldDisconnectCalls { get; private set; }
        public int CountdownCalls { get; private set; }
        public int MonstersShouldDespawnCalls { get; private set; }
        public int SmallestTribeCalls { get; private set; }
        public int WarConcludedCalls { get; private set; }

        public void OnCountdownAnnounced(short mapId, int remainingMinutes)
        {
            CountdownCalls++;
        }

        public void OnSmallestTribeFlagged(short mapId, byte tribeId)
        {
            SmallestTribeCalls++;
        }

        public void OnActiveWarStarted(short mapId)
        {
            ActiveWarStartedCalls++;
        }

        public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
            ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
        {
            WarConcludedCalls++;
            if (ThrowOnWarConcluded)
                throw new InvalidOperationException("simulated sink failure");
        }

        public void OnMonstersShouldDespawn(short mapId)
        {
            MonstersShouldDespawnCalls++;
        }

        public void OnAllSessionsShouldDisconnect(short mapId)
        {
            AllSessionsShouldDisconnectCalls++;
        }
    }
}
