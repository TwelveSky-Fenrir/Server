using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class LootBoxRewardResolver
{

        public const int RateScale = 10_000;

        public static int RollWeighted(Random random, ImmutableArray<WeightedReward> tiers)
    {
        var totalWeight = 0;
        foreach (var tier in tiers)
            totalWeight += tier.Weight;

        if (totalWeight <= 0)
            throw new ArgumentException("Weighted reward table must have at least one positive-weight entry.",
                nameof(tiers));

        var roll = random.Next(0, totalWeight);
        var cumulative = 0;
        foreach (var tier in tiers)
        {
            cumulative += tier.Weight;
            if (roll < cumulative)
                return tier.ItemId;
        }

        return tiers[^1].ItemId;
    }

        public static int RollBandedThenWeighted(Random random, ImmutableArray<RewardBand> bands,
        ImmutableArray<WeightedReward> fallbackTiers)
    {
        var roll = random.Next(0, RateScale);
        var cumulativeThreshold = 0;
        foreach (var band in bands)
        {
            cumulativeThreshold += band.ThresholdPer10000;
            if (roll < cumulativeThreshold)
                return band.RewardItemId;
        }

        return RollWeighted(random, fallbackTiers);
    }

        public static CloakRollResult RollCloak(Random random, int pityCounter, int pityCeiling,
        int guaranteedRewardItemId, int flatRareThresholdPer10000, int flatRareRewardItemId,
        ImmutableArray<WeightedReward> fallbackTiers)
    {
        var newPity = pityCounter + 1;
        if (newPity >= pityCeiling)
            return new CloakRollResult(guaranteedRewardItemId, 0, true);

        if (random.Next(0, RateScale) < flatRareThresholdPer10000)
            return new CloakRollResult(flatRareRewardItemId, newPity, false);

        return new CloakRollResult(RollWeighted(random, fallbackTiers), newPity, false);
    }

        public static int RollUniform(Random random, ImmutableArray<int> candidateItemIds)
    {
        if (candidateItemIds.IsDefaultOrEmpty)
            throw new ArgumentException("Uniform reward roll requires at least one candidate item id.",
                nameof(candidateItemIds));

        return candidateItemIds[random.Next(0, candidateItemIds.Length)];
    }

        public static int RollPools(Random random, ImmutableArray<RewardPool> pools)
    {
        if (pools.IsDefaultOrEmpty)
            throw new ArgumentException("Pool roll requires at least one pool.", nameof(pools));

        var maxCeiling = pools[^1].ThresholdCeilingInclusive;
        var roll = random.Next(0, maxCeiling + 1);
        foreach (var pool in pools)
            if (roll <= pool.ThresholdCeilingInclusive)
                return RollUniform(random, pool.Ids);

        return RollUniform(random, pools[^1].Ids);
    }

        public static int RollRareBandThenPools(Random random, ImmutableArray<RewardBand> rareBands,
        ImmutableArray<RewardPool> pools)
    {
        var roll = random.Next(0, RateScale);
        var cumulativeThreshold = 0;
        foreach (var band in rareBands)
        {
            cumulativeThreshold += band.ThresholdPer10000;
            if (roll < cumulativeThreshold)
                return band.RewardItemId;
        }

        return RollPools(random, pools);
    }

        public static PityStepResult PityStep(int pityCounter, int pityCeiling)
    {
        var next = pityCounter + 1;
        return next >= pityCeiling
            ? new PityStepResult(true, 0)
            : new PityStepResult(false, next);
    }

        public readonly record struct WeightedReward(int ItemId, int Weight);

        public readonly record struct RewardBand(int ThresholdPer10000, int RewardItemId);

        public readonly record struct RewardPool(int ThresholdCeilingInclusive, ImmutableArray<int> Ids);

    public readonly record struct CloakRollResult(int RewardItemId, int NewPityCounter, bool WasGuaranteed);

        public readonly record struct PityStepResult(bool Triggered, int NewCounter);
}
