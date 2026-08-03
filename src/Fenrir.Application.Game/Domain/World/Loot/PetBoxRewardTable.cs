using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class PetBoxRewardTable
{
    public const int BoxId = 602;

    public static readonly ImmutableArray<LootBoxRewardResolver.RewardPool> Pools =
    [
        new(20, [1178]),
        new(60, [1002, 1003, 1004, 1005]),
        new(120, [1190, 1491, 1492]),
        new(180, [506, 507, 508, 509, 578, 579]),
        new(199, [1103, 1118, 1145, 1166, 1222, 1237])
    ];

    public static readonly BoxRewardSpec Spec =
        BoxRewardSpec.RareBandThenPools(BoxId, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty, Pools);
}
