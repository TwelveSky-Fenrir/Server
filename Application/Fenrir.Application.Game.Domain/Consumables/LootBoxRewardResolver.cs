using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Generic weighted-reward roll primitives shared by the Mount/Pet/Cloak/Wing Lucky Box families and item
///     635's direct handler. Deliberately parameterized over the reward table rather than hard-coding any
///     catalog item id: the exact per-box "ordinary tier" reward lists (and, for Wing Box, the tribe-of-origin
///     keyed reward ids) were not reproduced item-by-item in the behavior contract this was built from --
///     only the top-level rare-tier rates were. Wiring this to real production item ids is a follow-up once
///     those concrete tables are extracted from the cited source range; this type is fully correct and tested
///     against fabricated tables in the meantime.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:878-983 (<c>RandomMount</c>, 0.5% = 50-in-10000 rare rate) ;
///     :985-1095 (<c>RandomPet</c>, 0.8% split 0.4%/0.4% between two ids) ; :1097-1171 (<c>RandomCloak</c>,
///     pity counter <c>gBox2249</c> forces a guaranteed reward at 100 opens and resets to zero, plus an
///     independent 0.6% flat roll) ; :1172-1281 (<c>RandomWingBox</c>, two bands at 0.8%/0.5%, tribe-of-origin
///     keyed, unrecognized tribe-of-origin fails outright) ; :1939-2024 (item 635's always-on direct handler,
///     flat 1-in-8 uniform roll across eight named tier-3 mount ids).
/// </remarks>
public static class LootBoxRewardResolver
{
    /// <summary>Every rate in this family is expressed as an integer "X-in-10000" (i.e. X basis points of a percent x100).</summary>
    public const int RateScale = 10_000;

    /// <summary>
    ///     Picks one entry from <paramref name="tiers" /> with probability proportional to its Weight (the
    ///     "five-tier"/"two-tier"/"four-tier" ordinary tables every box family falls back to once its own rare
    ///     band(s) miss). Throws if <paramref name="tiers" /> is empty or all weights are non-positive -- an
    ///     empty fallback table is a caller/catalog-wiring bug, not a roll outcome.
    /// </summary>
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

        // Unreachable given totalWeight above, but keeps the method total.
        return tiers[^1].ItemId;
    }

    /// <summary>
    ///     Mount Box (single rare band), Pet Box (two rare bands, 0.4%/0.4%), and Wing Box's own two bands all
    ///     share this exact shape: one shared draw checked against each band in order, first hit wins; a total
    ///     miss falls through to <paramref name="fallbackTiers" />.
    /// </summary>
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

    /// <summary>
    ///     Cloak Box: the pity counter is checked (and incremented) before any roll -- reaching
    ///     <paramref name="pityCeiling" /> (100 in the source) forces <paramref name="guaranteedRewardItemId" />
    ///     and resets the counter to zero, pre-empting both the flat rare roll and the ordinary table entirely.
    ///     Only when the pity counter has NOT just triggered does the independent flat-rate roll (0.6% in the
    ///     source) get a chance, falling back to <paramref name="fallbackTiers" /> on a further miss.
    /// </summary>
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

    /// <summary>
    ///     Item 635's always-on direct handler: a flat, uniform 1-in-N roll with no rarity tiers at all --
    ///     distinct from every other box family in this document, which all have at least one rare band.
    /// </summary>
    public static int RollUniform(Random random, ImmutableArray<int> candidateItemIds)
    {
        if (candidateItemIds.IsDefaultOrEmpty)
            throw new ArgumentException("Uniform reward roll requires at least one candidate item id.",
                nameof(candidateItemIds));

        return candidateItemIds[random.Next(0, candidateItemIds.Length)];
    }

    /// <summary>
    ///     The Mount Box (<c>RandomMount</c>) "ordinary tier" shape: a single small-range draw
    ///     (<c>rand % (N+1)</c> in the source, so inclusive 0..N) selects one of several pools by an ascending
    ///     inclusive ceiling -- the FIRST pool whose <see cref="RewardPool.ThresholdCeilingInclusive" /> is at or
    ///     above the draw wins -- then a uniform pick is made WITHIN the chosen pool. Distinct from
    ///     <see cref="RollWeighted" /> (which weights individual ids, not pools) and from
    ///     <see cref="RollBandedThenWeighted" /> (whose bands each resolve to one fixed id). The last pool's
    ///     ceiling is the draw's upper bound, so it always catches the remainder.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:878-983 (<c>RandomMount</c>'s five banded pools).</remarks>
    public static int RollPools(Random random, ImmutableArray<RewardPool> pools)
    {
        if (pools.IsDefaultOrEmpty)
            throw new ArgumentException("Pool roll requires at least one pool.", nameof(pools));

        var maxCeiling = pools[^1].ThresholdCeilingInclusive;
        var roll = random.Next(0, maxCeiling + 1);
        foreach (var pool in pools)
            if (roll <= pool.ThresholdCeilingInclusive)
                return RollUniform(random, pool.Ids);

        // Unreachable given the last ceiling equals maxCeiling, but keeps the method total.
        return RollUniform(random, pools[^1].Ids);
    }

    /// <summary>
    ///     Mount Box's full shape: one shared 0..<see cref="RateScale" /> draw is checked against each rare band
    ///     in order (first hit wins, e.g. the 0.5% id-635 band); a total miss falls through to the ordinary
    ///     banded pools via <see cref="RollPools" /> (which rolls its own, independent small-range draw).
    /// </summary>
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

    /// <summary>
    ///     The pure counter half of every persistent-pity box (<c>gBox8111</c>/<c>gBox8114</c>/<c>gBox8115</c>,
    ///     and the ceiling-100 <c>gBox2249</c> that <see cref="RollCloak" /> folds in directly): increments the
    ///     stored counter by one and reports whether that reached the ceiling. On trigger the counter resets to
    ///     zero and the caller must grant the box's guaranteed reward (a uniform pick over that box's guarantee
    ///     ids); otherwise the caller proceeds to its ordinary roll and stores <see cref="PityStepResult.NewCounter" />.
    ///     Kept separate from the roll so a box whose ordinary fallback pool is not yet catalogued can still be
    ///     wired the moment that pool lands -- the pity mechanic itself carries no fabricated ids.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1615-1621 (<c>gBox8111</c>, ceiling 200) ; :7866-7872
    ///     (<c>gBox8114</c>) ; :7923-7929 (<c>gBox8115</c>) -- each is "counter += 1; if (counter >= ceiling) {
    ///     force guaranteed reward; counter = 0; }".
    /// </remarks>
    public static PityStepResult PityStep(int pityCounter, int pityCeiling)
    {
        var next = pityCounter + 1;
        return next >= pityCeiling
            ? new PityStepResult(true, 0)
            : new PityStepResult(false, next);
    }

    /// <summary>
    ///     One entry of a weighted "ordinary tier" table: relative Weight, not a probability -- see
    ///     <see cref="RollWeighted" />.
    /// </summary>
    public readonly record struct WeightedReward(int ItemId, int Weight);

    /// <summary>One rare-tier band: rolls against a single shared draw, first matching band (in list order) wins.</summary>
    public readonly record struct RewardBand(int ThresholdPer10000, int RewardItemId);

    /// <summary>
    ///     One ordinary-tier pool for <see cref="RollPools" />: every id in the pool is equally likely once the
    ///     pool is selected. <see cref="ThresholdCeilingInclusive" /> is the ascending inclusive upper bound of
    ///     the pool-selection draw the pool claims (the previous pool's ceiling, exclusive, up to this one,
    ///     inclusive); the last pool's ceiling is also the draw's maximum.
    /// </summary>
    public readonly record struct RewardPool(int ThresholdCeilingInclusive, ImmutableArray<int> Ids);

    public readonly record struct CloakRollResult(int RewardItemId, int NewPityCounter, bool WasGuaranteed);

    /// <summary>
    ///     Result of <see cref="PityStep" />: <see cref="Triggered" /> means the guaranteed reward must be
    ///     granted and <see cref="NewCounter" /> is 0; otherwise <see cref="NewCounter" /> is the incremented
    ///     counter to persist alongside the ordinary reward.
    /// </summary>
    public readonly record struct PityStepResult(bool Triggered, int NewCounter);
}
