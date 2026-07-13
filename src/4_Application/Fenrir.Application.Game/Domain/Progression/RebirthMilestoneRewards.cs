using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Progression;

public static class RebirthMilestoneRewards
{

        public const int FirstMilestoneGeneration = 6;

        public const int SecondMilestoneGeneration = 12;

        public const int LevelToWorldDropSort = 40;

        private const int RewardQuantity = 0;

        public static RebirthMilestoneReward Resolve(int postIncrementRebirthCount, byte previousTribe)
    {
        return postIncrementRebirthCount switch
        {
            FirstMilestoneGeneration => new RebirthMilestoneReward(
                BuildRewardDrop(RewardItemIdAtSixthRebirth(previousTribe)), ClusterNotice: false),
            SecondMilestoneGeneration => new RebirthMilestoneReward(
                BuildRewardDrop(RewardItemIdAtTwelfthRebirth(previousTribe)), ClusterNotice: true),
            _ => new RebirthMilestoneReward(ImmutableArray<TribeGroundItemDrop>.Empty, ClusterNotice: false)
        };
    }

        public static string FormatTwelfthRebirthNotice(string characterName)
    {
        return $"{characterName} reached 12 rebirth!";
    }

    private static int RewardItemIdAtSixthRebirth(byte previousTribe)
    {
        return previousTribe switch
        {
            0 => 13553,
            1 => 33553,
            2 => 53553,
            _ => 0
        };
    }

    private static int RewardItemIdAtTwelfthRebirth(byte previousTribe)
    {
        return previousTribe switch
        {
            0 => 13554,
            1 => 33554,
            2 => 53554,
            _ => 0
        };
    }

    private static ImmutableArray<TribeGroundItemDrop> BuildRewardDrop(int rewardItemId)
    {
        return rewardItemId == 0
            ? ImmutableArray<TribeGroundItemDrop>.Empty
            : [new TribeGroundItemDrop(rewardItemId, RewardQuantity, LevelToWorldDropSort)];
    }
}

public readonly record struct RebirthMilestoneReward(
    ImmutableArray<TribeGroundItemDrop> Drops,
    bool ClusterNotice);
