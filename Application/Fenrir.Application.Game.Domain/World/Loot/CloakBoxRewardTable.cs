using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class CloakBoxRewardTable
{

        public const int BoxId = 2249;

        public const int PityCeiling = 100;

        public const int GuaranteedRewardItemId = 1401;

        public static readonly ImmutableArray<LootBoxRewardResolver.RewardBand> RareBands =
    [
        new(60, 1403)
    ];

        public static readonly ImmutableArray<int> PotionPoolIds = [506, 507, 508, 509, 578, 579];

        public static readonly ImmutableArray<int> ScrollCharmPoolIds = [1166, 1118, 1103, 1222, 1145, 1237];

        public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(100, PotionPoolIds),
        new(199, ScrollCharmPoolIds)
    ];

        public static readonly BoxRewardSpec Spec = BoxRewardSpec.RareBandThenPools(BoxId, RareBands, Pools);

        public static CloakBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
            return new CloakBoxRollResult(GuaranteedRewardItemId, pity.NewCounter, true);

        var rewardId = LootBoxRewardResolver.RollRareBandThenPools(random, RareBands, Pools);
        return new CloakBoxRollResult(rewardId, pity.NewCounter, false);
    }

        public readonly record struct CloakBoxRollResult(int RewardItemId, int NewPityCounter, bool WasPityTriggered);
}
