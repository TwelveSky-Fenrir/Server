using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class OverEnchantBox8113RewardTable
{

        public const int BoxItemId = 8113;

        public static readonly ImmutableArray<int> RewardItemIds =
        [8101, 8102, 8106, 1103, 1126, 1166, 1222, 1237, 828];

        public static readonly BoxRewardSpec Spec = BoxRewardSpec.Uniform(BoxItemId, RewardItemIds);
}
