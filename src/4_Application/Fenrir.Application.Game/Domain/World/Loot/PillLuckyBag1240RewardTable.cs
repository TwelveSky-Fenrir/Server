using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class PillLuckyBag1240RewardTable
{
    public const int BoxId = 1240;

    public static readonly ImmutableArray<int> RewardItemIds = [506, 508, 509, 578, 579];

    public static readonly BoxRewardSpec Spec = BoxRewardSpec.Uniform(BoxId, RewardItemIds);
}
