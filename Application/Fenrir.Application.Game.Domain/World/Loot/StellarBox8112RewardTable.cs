using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class StellarBox8112RewardTable
{

        public static readonly BoxRewardSpec Spec = BoxRewardSpec.Weighted(8112,
    [
        new LootBoxRewardResolver.WeightedReward(93500, 350),
        new LootBoxRewardResolver.WeightedReward(93501, 200),
        new LootBoxRewardResolver.WeightedReward(93502, 150),
        new LootBoxRewardResolver.WeightedReward(93503, 120),
        new LootBoxRewardResolver.WeightedReward(93504, 90),
        new LootBoxRewardResolver.WeightedReward(93505, 60),
        new LootBoxRewardResolver.WeightedReward(93506, 30)
    ]);
}
