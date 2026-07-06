using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

/// <summary>
///     Deterministic-RNG coverage for <see cref="LootBoxRewardResolver" />'s weighted-roll shapes. Every test
///     drives a <see cref="Random" /> stand-in that returns a fixed, caller-chosen value regardless of the
///     requested range -- this is what makes these tests deterministic rather than merely seeded (a seed alone
///     is not guaranteed stable across .NET runtime versions, since <see cref="Random" />'s algorithm is not a
///     documented contract).
/// </summary>
public class LootBoxRewardResolverTests
{
    private static readonly ImmutableArray<LootBoxRewardResolver.WeightedReward> ThreeTierTable =
    [
        new(100, 70), // cumulative [0,70)
        new(200, 20), // cumulative [70,90)
        new(300, 10) // cumulative [90,100)
    ];

    [Fact]
    public void RollWeighted_RollAtZero_PicksFirstTier()
    {
        var itemId = LootBoxRewardResolver.RollWeighted(new FixedRandom(0), ThreeTierTable);

        Assert.Equal(100, itemId);
    }

    [Fact]
    public void RollWeighted_RollAtFirstTierBoundary_PicksSecondTier()
    {
        var itemId = LootBoxRewardResolver.RollWeighted(new FixedRandom(70), ThreeTierTable);

        Assert.Equal(200, itemId);
    }

    [Fact]
    public void RollWeighted_RollAtLastTierBoundary_PicksThirdTier()
    {
        var itemId = LootBoxRewardResolver.RollWeighted(new FixedRandom(90), ThreeTierTable);

        Assert.Equal(300, itemId);
    }

    [Fact]
    public void RollWeighted_RollAtMaxValue_StillPicksThirdTier()
    {
        var itemId = LootBoxRewardResolver.RollWeighted(new FixedRandom(99), ThreeTierTable);

        Assert.Equal(300, itemId);
    }

    [Fact]
    public void RollWeighted_EmptyTable_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LootBoxRewardResolver.RollWeighted(new FixedRandom(0),
                ImmutableArray<LootBoxRewardResolver.WeightedReward>.Empty));
    }

    [Fact]
    public void RollWeighted_AllZeroWeights_Throws()
    {
        var table = ImmutableArray.Create(new LootBoxRewardResolver.WeightedReward(1, 0),
            new LootBoxRewardResolver.WeightedReward(2, 0));

        Assert.Throws<ArgumentException>(() => LootBoxRewardResolver.RollWeighted(new FixedRandom(0), table));
    }

    [Fact]
    public void RollBandedThenWeighted_RollWithinTheOnlyBand_ReturnsTheBandReward()
    {
        // Mount Box shape: single 0.5% (50-in-10000) rare band.
        var bands = ImmutableArray.Create(new LootBoxRewardResolver.RewardBand(50, 9999));

        var itemId = LootBoxRewardResolver.RollBandedThenWeighted(new FixedRandom(0), bands, ThreeTierTable);

        Assert.Equal(9999, itemId);
    }

    [Fact]
    public void RollBandedThenWeighted_RollJustAboveTheBand_FallsThroughToTheWeightedTable()
    {
        var bands = ImmutableArray.Create(new LootBoxRewardResolver.RewardBand(50, 9999));

        var itemId = LootBoxRewardResolver.RollBandedThenWeighted(new FixedRandom(50), bands, ThreeTierTable);

        Assert.Equal(100, itemId); // FixedRandom always returns 50 for the fallback roll too -> first tier of [0,70).
    }

    [Fact]
    public void RollBandedThenWeighted_TwoBands_SecondBandUsesCumulativeThreshold()
    {
        // Pet Box shape: two 0.4% bands (40+40-in-10000).
        var bands = ImmutableArray.Create(
            new LootBoxRewardResolver.RewardBand(40, 7001),
            new LootBoxRewardResolver.RewardBand(40, 7002));

        Assert.Equal(7001, LootBoxRewardResolver.RollBandedThenWeighted(new FixedRandom(39), bands, ThreeTierTable));
        Assert.Equal(7002, LootBoxRewardResolver.RollBandedThenWeighted(new FixedRandom(40), bands, ThreeTierTable));
        Assert.Equal(7002, LootBoxRewardResolver.RollBandedThenWeighted(new FixedRandom(79), bands, ThreeTierTable));
    }

    [Fact]
    public void RollCloak_PityCounterReachesCeiling_ReturnsGuaranteedReward_AndResetsCounterToZero()
    {
        var result = LootBoxRewardResolver.RollCloak(new FixedRandom(0), 99, 100,
            2249, 60, 5000,
            ThreeTierTable);

        Assert.Equal(2249, result.RewardItemId);
        Assert.Equal(0, result.NewPityCounter);
        Assert.True(result.WasGuaranteed);
    }

    [Fact]
    public void RollCloak_PityBelowCeiling_FlatRareHit_ReturnsRareReward_AndIncrementsPity()
    {
        var result = LootBoxRewardResolver.RollCloak(new FixedRandom(0), 10, 100,
            2249, 60, 5000,
            ThreeTierTable);

        Assert.Equal(5000, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasGuaranteed);
    }

    [Fact]
    public void RollCloak_PityBelowCeiling_FlatRareMiss_FallsToWeightedTable_AndIncrementsPity()
    {
        var result = LootBoxRewardResolver.RollCloak(new FixedRandom(60), 10, 100,
            2249, 60, 5000,
            ThreeTierTable);

        // FixedRandom(60) misses the 60-in-10000 threshold (roll < 60 required) and is then reused for the
        // fallback weighted roll, landing in the second tier of [0,70)/[70,90).
        Assert.Equal(100, result.RewardItemId);
        Assert.Equal(11, result.NewPityCounter);
        Assert.False(result.WasGuaranteed);
    }

    [Fact]
    public void RollUniform_PicksExactCandidateAtTheRolledIndex()
    {
        var candidates = ImmutableArray.Create(1307, 1310, 1313, 1316, 1319, 1322, 1325, 1328);

        Assert.Equal(1307, LootBoxRewardResolver.RollUniform(new FixedRandom(0), candidates));
        Assert.Equal(1328, LootBoxRewardResolver.RollUniform(new FixedRandom(7), candidates));
    }

    [Fact]
    public void RollUniform_EmptyCandidates_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LootBoxRewardResolver.RollUniform(new FixedRandom(0), ImmutableArray<int>.Empty));
    }

    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return value;
        }
    }
}
