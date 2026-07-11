using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Guards the item-8115 ("15% Mount Box" per in-code comment) reward + pity table (workstream
///     C10-remaining-box-pools) -- <see cref="MountVariantBox8115RewardTable" />. Single-open only, no
///     bulk-path counterpart; pity reward is a 50/50 coin flip (like box 8111, unlike box 8114's deterministic
///     guarantee).
/// </summary>
public class MountVariantBox8115RewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox8115_WithEmptyRareBandsAndNoRental()
    {
        var spec = MountVariantBox8115RewardTable.Spec;

        Assert.Equal(8115, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Same(MountVariantBox8115RewardTable.Pools, spec.Pools);
    }

    [Fact]
    public void Pools_AreSixBands_WithTheContractsExactCeilingsAndItemCounts()
    {
        var pools = MountVariantBox8115RewardTable.Pools;
        Assert.Equal(6, pools.Length);

        Assert.Equal(49, pools[0].ThresholdCeilingInclusive);
        Assert.Equal([1307, 1315, 1319], pools[0].Ids.ToArray());

        Assert.Equal(69, pools[1].ThresholdCeilingInclusive);
        Assert.Equal([1308, 1309, 1322, 1325, 1328], pools[1].Ids.ToArray());

        Assert.Equal(79, pools[2].ThresholdCeilingInclusive);
        Assert.Equal([1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326], pools[2].Ids.ToArray());

        Assert.Equal(99, pools[3].ThresholdCeilingInclusive);
        Assert.Equal([611, 612, 652], pools[3].Ids.ToArray());

        Assert.Equal(129, pools[4].ThresholdCeilingInclusive);
        Assert.Equal([506, 507, 508, 509, 578, 579], pools[4].Ids.ToArray());

        Assert.Equal(149, pools[5].ThresholdCeilingInclusive);
        Assert.Equal([1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106], pools[5].Ids.ToArray());
    }

    [Fact]
    public void PityCeiling_Is200_AndPityRewardPool_IsExactly1307And1315()
    {
        Assert.Equal(200, MountVariantBox8115RewardTable.PityCeiling);
        Assert.Equal([1307, 1315], MountVariantBox8115RewardTable.PityRewardItemIds.ToArray());
    }

    [Fact]
    public void Roll_PityCounterAt199_Triggers_ConsumesExactlyOneDraw_ForTheCoinFlip()
    {
        var result = MountVariantBox8115RewardTable.Roll(199, new ScriptedRandom(0));

        Assert.Equal(1307, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterAt199_CoinFlipTopHalf_ReturnsTheOtherId()
    {
        var result = MountVariantBox8115RewardTable.Roll(199, new ScriptedRandom(1));

        Assert.Equal(1315, result.RewardItemId);
        Assert.True(result.WasPityTriggered);
    }

    [Theory]
    [InlineData(0, 1307)]
    [InlineData(49, 1307)]
    [InlineData(50, 1308)]
    [InlineData(69, 1308)]
    [InlineData(70, 1301)]
    [InlineData(79, 1301)]
    [InlineData(80, 611)]
    [InlineData(99, 611)]
    [InlineData(100, 506)]
    [InlineData(129, 506)]
    [InlineData(130, 1166)]
    [InlineData(149, 1166)]
    public void Roll_PityBelowCeiling_RegularRollBoundary_SelectsExpectedPoolFirstId(int poolSelectDraw,
        int expectedRewardId)
    {
        var result = MountVariantBox8115RewardTable.Roll(10, new ScriptedRandom(poolSelectDraw, 0));

        Assert.Equal(expectedRewardId, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException(
                    "ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
