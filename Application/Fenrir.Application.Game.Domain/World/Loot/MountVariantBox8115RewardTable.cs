using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class MountVariantBox8115RewardTable
{

        public const int BoxId = 8115;

        public const int PityCeiling = 200;

        public static readonly ImmutableArray<int> PityRewardItemIds = [1307, 1315];

        public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(49, [1307, 1315, 1319]),
        new(69, [1308, 1309, 1322, 1325, 1328]),
        new(79, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
        new(99, [611, 612, 652]),
        new(129, [506, 507, 508, 509, 578, 579]),
        new(149, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

        public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

        public static MountVariantBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
        {
            var guaranteed = LootBoxRewardResolver.RollUniform(random, PityRewardItemIds);
            return new MountVariantBoxRollResult(guaranteed, pity.NewCounter, true);
        }

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new MountVariantBoxRollResult(rewardId, pity.NewCounter, false);
    }

        public readonly record struct MountVariantBoxRollResult(int RewardItemId, int NewPityCounter,
        bool WasPityTriggered);
}
