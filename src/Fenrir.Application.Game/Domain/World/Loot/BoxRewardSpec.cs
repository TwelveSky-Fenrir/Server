using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

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

    public int BoxId { get; }

    public BoxRewardKind Kind { get; }

    public ImmutableArray<int> UniformIds { get; }

    public ImmutableArray<LootBoxRewardResolver.WeightedReward> WeightedRewards { get; }

    public ImmutableArray<LootBoxRewardResolver.RewardBand> RareBands { get; }

    public ImmutableArray<LootBoxRewardResolver.RewardPool> Pools { get; }

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

public enum BoxRewardKind
{
    Uniform,

    Weighted,

    RareBandThenPools
}
