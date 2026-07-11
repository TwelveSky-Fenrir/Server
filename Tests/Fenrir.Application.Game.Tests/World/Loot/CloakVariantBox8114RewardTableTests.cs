using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Guards the item-8114 reward + pity table (workstream C10-remaining-box-pools) --
///     <see cref="CloakVariantBox8114RewardTable" />. Single-open only (no bulk counterpart in the legacy
///     source); pity reward is deterministic (id 1403, zero draws), unlike box 8111/8115's coin-flip guarantee.
/// </summary>
public class CloakVariantBox8114RewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox8114_WithEmptyRareBandsAndNoRental()
    {
        var spec = CloakVariantBox8114RewardTable.Spec;

        Assert.Equal(8114, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Empty(spec.RareBands);
        Assert.Same(CloakVariantBox8114RewardTable.Pools, spec.Pools);
    }

    [Fact]
    public void Pools_AreFourBands_WithTheContractsExactCeilingsAndItemCounts()
    {
        var pools = CloakVariantBox8114RewardTable.Pools;
        Assert.Equal(4, pools.Length);

        Assert.Equal(1, pools[0].ThresholdCeilingInclusive);
        Assert.Equal([1403], pools[0].Ids.ToArray());

        Assert.Equal(49, pools[1].ThresholdCeilingInclusive);
        Assert.Equal([92290, 1401], pools[1].Ids.ToArray());

        Assert.Equal(249, pools[2].ThresholdCeilingInclusive);
        Assert.Equal([506, 507, 508, 509, 578, 579], pools[2].Ids.ToArray());

        Assert.Equal(499, pools[3].ThresholdCeilingInclusive);
        Assert.Equal([1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106], pools[3].Ids.ToArray());
    }

    [Fact]
    public void PityCeiling_Is200_AndGuaranteedReward_Is1403()
    {
        Assert.Equal(200, CloakVariantBox8114RewardTable.PityCeiling);
        Assert.Equal(1403, CloakVariantBox8114RewardTable.GuaranteedRewardItemId);
    }

    [Fact]
    public void Roll_PityCounterAt199_Triggers_ConsumesZeroDraws_AndResetsToZero()
    {
        // No scripted values at all: ScriptedRandom throws if Next is ever called, proving the deterministic
        // pity branch never touches random (unlike box 8111/8115's coin-flip guarantee).
        var result = CloakVariantBox8114RewardTable.Roll(199, new ScriptedRandom());

        Assert.Equal(1403, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterWellPastCeiling_StillTriggers()
    {
        var result = CloakVariantBox8114RewardTable.Roll(500, new ScriptedRandom());

        Assert.Equal(1403, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Theory]
    [InlineData(0, 1403)]
    [InlineData(1, 1403)]
    [InlineData(2, 92290)]
    [InlineData(49, 92290)]
    [InlineData(50, 506)]
    [InlineData(249, 506)]
    [InlineData(250, 1166)]
    [InlineData(499, 1166)]
    public void Roll_PityBelowCeiling_RegularRollBoundary_SelectsExpectedPoolFirstId(int poolSelectDraw,
        int expectedRewardId)
    {
        var result = CloakVariantBox8114RewardTable.Roll(10, new ScriptedRandom(poolSelectDraw, 0));

        Assert.Equal(expectedRewardId, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_NaturalHitOnItem1403_IsAGenuinelyIndependentOutcome_NotThePityBranch()
    {
        // Pool-select draw 0 lands in pool 1 (item 1403) even though pity has NOT triggered -- confirming the
        // "reachable naturally too" claim from this type's own remarks.
        var result = CloakVariantBox8114RewardTable.Roll(0, new ScriptedRandom(0, 0));

        Assert.Equal(1403, result.RewardItemId);
        Assert.False(result.WasPityTriggered);
        Assert.Equal(1, result.NewPityCounter);
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
