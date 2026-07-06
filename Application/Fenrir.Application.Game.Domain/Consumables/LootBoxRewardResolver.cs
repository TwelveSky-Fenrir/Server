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

    /// <summary>One entry of a weighted "ordinary tier" table: relative Weight, not a probability -- see <see cref="RollWeighted" />.</summary>
    public readonly record struct WeightedReward(int ItemId, int Weight);

    /// <summary>One rare-tier band: rolls against a single shared draw, first matching band (in list order) wins.</summary>
    public readonly record struct RewardBand(int ThresholdPer10000, int RewardItemId);

    public readonly record struct CloakRollResult(int RewardItemId, int NewPityCounter, bool WasGuaranteed);

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
}
