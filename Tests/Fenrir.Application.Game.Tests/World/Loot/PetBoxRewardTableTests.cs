using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class PetBoxRewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox602()
    {
        var spec = PetBoxRewardTable.Spec;

        Assert.Equal(602, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.True(spec.RareBands == PetBoxRewardTable.RareBands);
        Assert.True(spec.Pools == PetBoxRewardTable.Pools);
    }

    [Fact]
    public void RareBands_AreTwoBandsOf40EachSummingTo80()
    {
        Assert.Equal(2, PetBoxRewardTable.RareBands.Length);

        Assert.Equal(40, PetBoxRewardTable.RareBands[0].ThresholdPer10000);
        Assert.Equal(1012, PetBoxRewardTable.RareBands[0].RewardItemId);

        Assert.Equal(40, PetBoxRewardTable.RareBands[1].ThresholdPer10000);
        Assert.Equal(1016, PetBoxRewardTable.RareBands[1].RewardItemId);
    }

    [Fact]
    public void Pools_AreFiveBands_WithTheContractsExactCeilingsAndItemCounts()
    {
        var pools = PetBoxRewardTable.Pools;
        Assert.Equal(5, pools.Length);

        Assert.Equal(20, pools[0].ThresholdCeilingInclusive);
        Assert.Single(pools[0].Ids);
        Assert.Equal(1178, pools[0].Ids[0]);

        Assert.Equal(60, pools[1].ThresholdCeilingInclusive);
        Assert.Equal(4, pools[1].Ids.Length);
        Assert.Contains(1002, pools[1].Ids);
        Assert.Contains(1003, pools[1].Ids);
        Assert.Contains(1004, pools[1].Ids);
        Assert.Contains(1005, pools[1].Ids);

        Assert.Equal(120, pools[2].ThresholdCeilingInclusive);
        Assert.Equal(3, pools[2].Ids.Length);
        Assert.Contains(1190, pools[2].Ids);
        Assert.Contains(1491, pools[2].Ids);
        Assert.Contains(1492, pools[2].Ids);

        Assert.Equal(180, pools[3].ThresholdCeilingInclusive);
        Assert.Equal(6, pools[3].Ids.Length);
        int[] band4Expected = [506, 507, 508, 509, 578, 579];
        Assert.Equal(band4Expected, pools[3].Ids.ToArray());

        Assert.Equal(199, pools[4].ThresholdCeilingInclusive);
        Assert.Equal(6, pools[4].Ids.Length);
        int[] band5Expected = [1103, 1118, 1145, 1166, 1222, 1237];
        Assert.Equal(band5Expected, pools[4].Ids.ToArray());
    }

    [Fact]
    public void PoolWidths_MatchTheContractsBandWidthTable_21_40_60_60_19()
    {
        var pools = PetBoxRewardTable.Pools;

        Assert.Equal(21, pools[0].ThresholdCeilingInclusive + 1);
        Assert.Equal(40, pools[1].ThresholdCeilingInclusive - pools[0].ThresholdCeilingInclusive);
        Assert.Equal(60, pools[2].ThresholdCeilingInclusive - pools[1].ThresholdCeilingInclusive);
        Assert.Equal(60, pools[3].ThresholdCeilingInclusive - pools[2].ThresholdCeilingInclusive);
        Assert.Equal(19, pools[4].ThresholdCeilingInclusive - pools[3].ThresholdCeilingInclusive);

        Assert.Equal(199, pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void RollRareBandThenPools_BottomHalfDraw_ReturnsFirstSpecialId_WithoutTouchingPools()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(0), PetBoxRewardTable.RareBands,
            PetBoxRewardTable.Pools);

        Assert.Equal(1012, id);
    }

    [Fact]
    public void RollRareBandThenPools_TopHalfDraw_ReturnsSecondSpecialId()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(40), PetBoxRewardTable.RareBands,
            PetBoxRewardTable.Pools);

        Assert.Equal(1016, id);
    }

    [Fact]
    public void RollRareBandThenPools_JustAboveSpecialTier_FallsThroughToFirstFallbackBand()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(80, 0, 0),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1178, id);
    }

    [Fact]
    public void RollRareBandThenPools_MaxFallbackDraw_ReturnsFromLastBand()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 199, 5),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1237, id);
    }

    [Fact]
    public void RollRareBandThenPools_MidFallbackDraw_LandsInThirdBand()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 61, 1),
            PetBoxRewardTable.RareBands, PetBoxRewardTable.Pools);

        Assert.Equal(1491, id);
    }

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
