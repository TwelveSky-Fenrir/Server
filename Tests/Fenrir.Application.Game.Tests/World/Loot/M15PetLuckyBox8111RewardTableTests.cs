using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Guards the item-8111 "M15 Pet Lucky Box" reward + pity table (workstream C10-remaining-box-pools) --
///     <see cref="M15PetLuckyBox8111RewardTable" />. Exercises <see cref="M15PetLuckyBox8111RewardTable.Roll" />
///     directly (not through <see cref="LootBoxCatalog" />/<see cref="LootBoxOpenResolver" />), the same
///     isolation <see cref="CloakBoxRewardTable" />'s own tests use -- this box's pity gate is threaded through
///     <c>LootBoxUseItemHandler.ResolveRewardIdOverride</c>, not through <see cref="M15PetLuckyBox8111RewardTable.Spec" />
///     directly.
/// </summary>
public class M15PetLuckyBox8111RewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox8111_WithEmptyRareBandsAndNoRental()
    {
        var spec = M15PetLuckyBox8111RewardTable.Spec;

        Assert.Equal(8111, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Same(M15PetLuckyBox8111RewardTable.Pools, spec.Pools);
    }

    [Fact]
    public void Pools_AreFiveBands_WithTheContractsExactCeilingsAndItemCounts()
    {
        var pools = M15PetLuckyBox8111RewardTable.Pools;
        Assert.Equal(5, pools.Length);

        Assert.Equal(50, pools[0].ThresholdCeilingInclusive);
        Assert.Equal([1012], pools[0].Ids.ToArray());

        Assert.Equal(80, pools[1].ThresholdCeilingInclusive);
        Assert.Equal([1013, 1014, 1015], pools[1].Ids.ToArray());

        Assert.Equal(100, pools[2].ThresholdCeilingInclusive);
        Assert.Equal([1190, 1491, 1492], pools[2].Ids.ToArray());

        Assert.Equal(130, pools[3].ThresholdCeilingInclusive);
        Assert.Equal([506, 507, 508, 509, 578, 579], pools[3].Ids.ToArray());

        Assert.Equal(149, pools[4].ThresholdCeilingInclusive);
        Assert.Equal([1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106], pools[4].Ids.ToArray());
    }

    [Fact]
    public void PityCeiling_Is200_AndPityRewardPool_IsExactly1012And1016()
    {
        Assert.Equal(200, M15PetLuckyBox8111RewardTable.PityCeiling);
        Assert.Equal([1012, 1016], M15PetLuckyBox8111RewardTable.PityRewardItemIds.ToArray());
    }

    [Fact]
    public void Roll_PityCounterAt199_Triggers_ConsumesExactlyOneDraw_ForTheCoinFlip_AndResetsToZero()
    {
        // Exactly one scripted value: ScriptedRandom throws if a second draw were consumed, proving the
        // guaranteed-reward coin flip -- unlike box 2249/8114's zero-draw guarantee -- spends exactly one draw.
        var result = M15PetLuckyBox8111RewardTable.Roll(199, new ScriptedRandom(0));

        Assert.Equal(1012, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterAt199_CoinFlipTopHalf_ReturnsTheOtherId()
    {
        var result = M15PetLuckyBox8111RewardTable.Roll(199, new ScriptedRandom(1));

        Assert.Equal(1016, result.RewardItemId);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterWellPastCeiling_StillTriggers()
    {
        var result = M15PetLuckyBox8111RewardTable.Roll(250, new ScriptedRandom(0));

        Assert.Equal(1012, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Theory]
    [InlineData(0, 1012)]
    [InlineData(50, 1012)]
    [InlineData(51, 1013)]
    [InlineData(80, 1013)]
    [InlineData(81, 1190)]
    [InlineData(100, 1190)]
    [InlineData(101, 506)]
    [InlineData(130, 506)]
    [InlineData(131, 1166)]
    [InlineData(149, 1166)]
    public void Roll_PityBelowCeiling_RegularRollBoundary_SelectsExpectedPoolFirstId(int poolSelectDraw,
        int expectedRewardId)
    {
        // Within-pool uniform draw scripted as 0 -> first id in whichever pool the boundary lands in.
        var result = M15PetLuckyBox8111RewardTable.Roll(10, new ScriptedRandom(poolSelectDraw, 0));

        Assert.Equal(expectedRewardId, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SinglePool_IgnoresWithinPoolDraw_AlwaysReturns1012()
    {
        // Pool 1 has only one id -- RollUniform still consumes a within-pool draw, but any value maps to 1012.
        var result = M15PetLuckyBox8111RewardTable.Roll(5, new ScriptedRandom(0, 0));

        Assert.Equal(1012, result.RewardItemId);
        Assert.Equal(6, result.NewPityCounter);
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
