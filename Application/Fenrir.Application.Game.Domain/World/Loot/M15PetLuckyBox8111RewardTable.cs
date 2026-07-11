using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class M15PetLuckyBox8111RewardTable
{

        public const int BoxId = 8111;

        public const int PityCeiling = 200;

        public static readonly ImmutableArray<int> PityRewardItemIds = [1012, 1016];

        public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(50, [1012]),
        new(80, [1013, 1014, 1015]),
        new(100, [1190, 1491, 1492]),
        new(130, [506, 507, 508, 509, 578, 579]),
        new(149, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

        public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

        public static M15PetLuckyBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
        {
            var guaranteed = LootBoxRewardResolver.RollUniform(random, PityRewardItemIds);
            return new M15PetLuckyBoxRollResult(guaranteed, pity.NewCounter, true);
        }

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new M15PetLuckyBoxRollResult(rewardId, pity.NewCounter, false);
    }

        public readonly record struct M15PetLuckyBoxRollResult(int RewardItemId, int NewPityCounter,
        bool WasPityTriggered);
}
