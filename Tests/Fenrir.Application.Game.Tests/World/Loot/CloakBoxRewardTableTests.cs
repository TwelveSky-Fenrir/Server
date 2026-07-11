using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class CloakBoxRewardTableTests
{
    [Fact]
    public void Spec_IsRareBandThenPools_ForBox2249_PostPityShapeOnly()
    {
        var spec = CloakBoxRewardTable.Spec;

        Assert.Equal(2249, spec.BoxId);
        Assert.Equal(BoxRewardKind.RareBandThenPools, spec.Kind);
        Assert.Equal(0, spec.RentalDays);
        Assert.Equal(CloakBoxRewardTable.RareBands, spec.RareBands);
        Assert.Equal(CloakBoxRewardTable.Pools, spec.Pools);
    }

    [Fact]
    public void RareBand_IsSingleBand_60In10000_ToWarlordCape1403()
    {
        Assert.Single(CloakBoxRewardTable.RareBands);
        Assert.Equal(60, CloakBoxRewardTable.RareBands[0].ThresholdPer10000);
        Assert.Equal(1403, CloakBoxRewardTable.RareBands[0].RewardItemId);
    }

    [Fact]
    public void Pools_AreTwoBands_PotionThenScrollCharm_WithTheContractsExactIds()
    {
        var pools = CloakBoxRewardTable.Pools;
        Assert.Equal(2, pools.Length);

        Assert.Equal(100, pools[0].ThresholdCeilingInclusive);
        int[] potionExpected = [506, 507, 508, 509, 578, 579];
        Assert.Equal(potionExpected, pools[0].Ids.ToArray());

        Assert.Equal(199, pools[1].ThresholdCeilingInclusive);
        int[] charmExpected = [1166, 1118, 1103, 1222, 1145, 1237];
        Assert.Equal(charmExpected, pools[1].Ids.ToArray());
    }

    [Fact]
    public void PoolWidths_Are101And99_Spanning0To199Inclusive_MatchingContractsExactWording()
    {
        var pools = CloakBoxRewardTable.Pools;

        Assert.Equal(101, pools[0].ThresholdCeilingInclusive + 1);
        Assert.Equal(99, pools[1].ThresholdCeilingInclusive - pools[0].ThresholdCeilingInclusive);

        Assert.Equal(199, pools[^1].ThresholdCeilingInclusive);
    }

    [Fact]
    public void PityCeiling_Is100_AndGuaranteedReward_Is1401UltimateCloak()
    {
        Assert.Equal(100, CloakBoxRewardTable.PityCeiling);
        Assert.Equal(1401, CloakBoxRewardTable.GuaranteedRewardItemId);
        Assert.Equal(2249, CloakBoxRewardTable.BoxId);
    }

    [Fact]
    public void Roll_PityCounterAt99_TriggersGuaranteedReward_ResetsToZero_AndConsumesZeroRandomDraws()
    {
        var result = CloakBoxRewardTable.Roll(99, new ScriptedRandom());

        Assert.Equal(1401, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityCounterWellPastCeiling_StillTriggers_ReachedNotOnlyEqualled()
    {
        var result = CloakBoxRewardTable.Roll(150, new ScriptedRandom());

        Assert.Equal(1401, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_LegacyPreMigrationCounterMinusOne_FirstOpenDoesNotTrigger_NeedsOneExtraOpen()
    {
        var result = CloakBoxRewardTable.Roll(-1, new ScriptedRandom(9999, 199, 5));

        Assert.False(result.WasPityTriggered);
        Assert.Equal(0, result.NewPityCounter);
        Assert.Equal(1237, result.RewardItemId);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollHits_ReturnsWarlordCape_IncrementsCounter_AndNeverTouchesPools()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(0));

        Assert.Equal(1403, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_PotionPoolLowBoundary_Draw0SelectsFirstPotionId()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(60, 0, 0));

        Assert.Equal(506, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasPityTriggered);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_PotionPoolHighBoundary_Draw100SelectsLastPotionId()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 100, 5));

        Assert.Equal(579, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_CharmPoolLowBoundary_Draw101SelectsFirstCharmId()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 101, 0));

        Assert.Equal(1166, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void Roll_PityBelowCeiling_SpecialRollMisses_CharmPoolHighBoundary_Draw199SelectsLastCharmId()
    {
        var result = CloakBoxRewardTable.Roll(10, new ScriptedRandom(9999, 199, 5));

        Assert.Equal(1237, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
    }

    [Fact]
    public void RollRareBandThenPools_DirectlyAgainstTheTable_MatchesTheSameShapeMountAndPetBoxesUse()
    {
        var id = LootBoxRewardResolver.RollRareBandThenPools(new ScriptedRandom(9999, 0, 3),
            CloakBoxRewardTable.RareBands, CloakBoxRewardTable.Pools);

        Assert.Equal(509, id);
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
