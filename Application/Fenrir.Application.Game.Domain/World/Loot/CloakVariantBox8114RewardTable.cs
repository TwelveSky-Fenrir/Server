using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class CloakVariantBox8114RewardTable
{
    public const int BoxId = 8114;

    public const int PityCeiling = 200;

    public const int GuaranteedRewardItemId = 1403;

    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(1, [1403]),
        new(49, [92290, 1401]),
        new(249, [506, 507, 508, 509, 578, 579]),
        new(499, [1166, 1118, 1103, 1222, 1145, 1237, 8101, 8102, 8106])
    ];

    public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);

    public static CloakVariantBoxRollResult Roll(int currentPityCounter, Random random)
    {
        var pity = LootBoxRewardResolver.PityStep(currentPityCounter, PityCeiling);
        if (pity.Triggered)
            return new CloakVariantBoxRollResult(GuaranteedRewardItemId, pity.NewCounter, true);

        var rewardId = LootBoxRewardResolver.RollPools(random, Pools);
        return new CloakVariantBoxRollResult(rewardId, pity.NewCounter, false);
    }

    public readonly record struct CloakVariantBoxRollResult(
        int RewardItemId,
        int NewPityCounter,
        bool WasPityTriggered);
}
