using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="SimulationClock.RebroadcastStaggerOffset" /> -- the deterministic per-entity phase
///     offset that fixes the confirmed monster-keep-alive thundering-herd bug (every monster spawned by a
///     zone's initial spawn-region population shared the exact same <c>LastRebroadcastAt</c>, so all of them
///     became due for <c>Zone.RebroadcastMonsters</c> on the exact same later tick, forever).
/// </summary>
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
        // Bucketed in whole legacy-tick units (10 buckets for the 5 s monster interval) -- consecutive ids
        // 1..10 must visit all 10 buckets, spread across the FULL window, not just its opening moment.
        var offsets = new HashSet<TimeSpan>();
        for (var id = 1; id <= 10; id++)
            offsets.Add(SimulationClock.RebroadcastStaggerOffset(id, SimulationClock.MonsterRebroadcastInterval));

        Assert.Equal(10, offsets.Count);
        Assert.Contains(offsets, o => o >= SimulationClock.MonsterRebroadcastInterval / 2);
    }

    [Fact]
    public void EntityIdsOneBucketCountApart_ProduceTheExactSameOffset_ByDesign()
    {
        // bucketCount for a 5 s interval at a 500 ms legacy tick is 10 -- id and id+10 alias to the same
        // bucket. This is an accepted, documented tradeoff (see the method's own remarks), not a bug: the
        // population is still spread across all 10 real evaluation passes, just with more than one entity per
        // pass once the population exceeds the bucket count.
        var a = SimulationClock.RebroadcastStaggerOffset(3, SimulationClock.MonsterRebroadcastInterval);
        var b = SimulationClock.RebroadcastStaggerOffset(13, SimulationClock.MonsterRebroadcastInterval);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NegativeEntityId_StillProducesAValidInWindowOffset()
    {
        // Tower guardians and other reserved-negative-ServerIndex monsters (Zone.Monsters.cs's own remarks)
        // must not throw or produce a negative/out-of-range offset.
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
