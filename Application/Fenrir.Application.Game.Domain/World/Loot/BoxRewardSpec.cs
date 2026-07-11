using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     One box id's reward-table shape for <see cref="LootBoxCatalog" />. A hand-discriminated union
///     (<see cref="Kind" /> decides which of the reward-table fields is meaningful) rather than a class
///     hierarchy -- the same "one struct, a discriminator, no per-open allocation" idiom
///     <c>ZoneCommand</c>/<c>BoxRewardPlacementResolver.Result</c> use. Immutable and built once at catalog
///     construction; <see cref="RollRewardId" /> is a pure read that delegates to the already-tested
///     <see cref="LootBoxRewardResolver" /> primitives.
/// </summary>
public sealed record BoxRewardSpec
{
    private BoxRewardSpec(int boxId, BoxRewardKind kind,
        ImmutableArray<int> uniformIds,
        ImmutableArray<LootBoxRewardResolver.WeightedReward> weighted,
        ImmutableArray<LootBoxRewardResolver.RewardBand> rareBands,
        ImmutableArray<LootBoxRewardResolver.RewardPool> pools,
        int rentalDays)
    {
        BoxId = boxId;
        Kind = kind;
        UniformIds = uniformIds;
        WeightedRewards = weighted;
        RareBands = rareBands;
        Pools = pools;
        RentalDays = rentalDays;
    }

    /// <summary>The box's world.Items id (the item sitting in the opened inventory slot).</summary>
    public int BoxId { get; }

    /// <summary>Which reward-table field below is meaningful.</summary>
    public BoxRewardKind Kind { get; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="BoxRewardKind.Uniform" />.</summary>
    public ImmutableArray<int> UniformIds { get; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="BoxRewardKind.Weighted" />.</summary>
    public ImmutableArray<LootBoxRewardResolver.WeightedReward> WeightedRewards { get; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="BoxRewardKind.RareBandThenPools" />.</summary>
    public ImmutableArray<LootBoxRewardResolver.RewardBand> RareBands { get; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="BoxRewardKind.RareBandThenPools" />.</summary>
    public ImmutableArray<LootBoxRewardResolver.RewardPool> Pools { get; }

    /// <summary>
    ///     Rental days stamped onto a non-stackable reward's expiry date (0 = no rental, the common case). Only
    ///     applied to a non-stackable reward: a stackable reward is stripped of expiry entirely, per the box
    ///     placement contract.
    /// </summary>
    public int RentalDays { get; }

    public static BoxRewardSpec Uniform(int boxId, ImmutableArray<int> ids, int rentalDays = 0)
    {
        return new BoxRewardSpec(boxId, BoxRewardKind.Uniform, ids, default, default, default, rentalDays);
    }

    public static BoxRewardSpec Weighted(int boxId, ImmutableArray<LootBoxRewardResolver.WeightedReward> tiers,
        int rentalDays = 0)
    {
        return new BoxRewardSpec(boxId, BoxRewardKind.Weighted, default, tiers, default, default, rentalDays);
    }

    public static BoxRewardSpec RareBandThenPools(int boxId,
        ImmutableArray<LootBoxRewardResolver.RewardBand> rareBands,
        ImmutableArray<LootBoxRewardResolver.RewardPool> pools, int rentalDays = 0)
    {
        return new BoxRewardSpec(boxId, BoxRewardKind.RareBandThenPools, default, default, rareBands, pools,
            rentalDays);
    }

    /// <summary>Rolls one reward item id from this box's table using <paramref name="random" />.</summary>
    public int RollRewardId(Random random)
    {
        return Kind switch
        {
            BoxRewardKind.Uniform => LootBoxRewardResolver.RollUniform(random, UniformIds),
            BoxRewardKind.Weighted => LootBoxRewardResolver.RollWeighted(random, WeightedRewards),
            BoxRewardKind.RareBandThenPools => LootBoxRewardResolver.RollRareBandThenPools(random, RareBands, Pools),
            _ => throw new InvalidOperationException($"Unhandled box reward kind {Kind} for box {BoxId}.")
        };
    }
}

/// <summary>Discriminator for <see cref="BoxRewardSpec" />.</summary>
public enum BoxRewardKind
{
    /// <summary>A flat uniform pick over <see cref="BoxRewardSpec.UniformIds" />.</summary>
    Uniform,

    /// <summary>A weighted pick over <see cref="BoxRewardSpec.WeightedRewards" /> (cumulative spans).</summary>
    Weighted,

    /// <summary>A rare-band draw over <see cref="BoxRewardSpec.RareBands" />, else banded pools.</summary>
    RareBandThenPools
}
