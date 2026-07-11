using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

public class SimulationClockStaggerTests
{
    [Fact]
    public void Offset_IsAlwaysStrictlyLessThanTheInterval_SoTheExistingAtLeastOnceEveryIntervalContractHolds()
    {
        for (var id = 0; id < 500; id++)
        {
            var offset = SimulationClock.RebroadcastStaggerOffset(id, SimulationClock.MonsterRebroadcastInterval);
            Assert.True(offset >= TimeSpan.Zero);
            Assert.True(offset < SimulationClock.MonsterRebroadcastInterval);
        }
    }

    [Fact]
    public void ConsecutiveEntityIds_SpreadLinearlyAcrossTheWholeWindow_NotClusteredAtOneEnd()
    {
        var offsets = new HashSet<TimeSpan>();
        for (var id = 1; id <= 10; id++)
            offsets.Add(SimulationClock.RebroadcastStaggerOffset(id, SimulationClock.MonsterRebroadcastInterval));

        Assert.Equal(10, offsets.Count);
        Assert.Contains(offsets, o => o >= SimulationClock.MonsterRebroadcastInterval / 2);
    }

    [Fact]
    public void EntityIdsOneBucketCountApart_ProduceTheExactSameOffset_ByDesign()
    {
        var a = SimulationClock.RebroadcastStaggerOffset(3, SimulationClock.MonsterRebroadcastInterval);
        var b = SimulationClock.RebroadcastStaggerOffset(13, SimulationClock.MonsterRebroadcastInterval);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NegativeEntityId_StillProducesAValidInWindowOffset()
    {
        for (var id = -50; id < 0; id++)
        {
            var offset = SimulationClock.RebroadcastStaggerOffset(id, SimulationClock.MonsterRebroadcastInterval);
            Assert.True(offset >= TimeSpan.Zero);
            Assert.True(offset < SimulationClock.MonsterRebroadcastInterval);
        }
    }

    [Fact]
    public void ZeroOrNegativeInterval_ReturnsZero_RatherThanDividingByZero()
    {
        Assert.Equal(TimeSpan.Zero, SimulationClock.RebroadcastStaggerOffset(7, TimeSpan.Zero));
        Assert.Equal(TimeSpan.Zero, SimulationClock.RebroadcastStaggerOffset(7, TimeSpan.FromMilliseconds(-100)));
    }
}
