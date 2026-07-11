using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedEndOfBattleRewardCalculator
{

        public static ImmutableArray<RankReward> ComputeRankRewards(ImmutableArray<KillFeedRankedEntry> topThree,
        bool isFfaMap, bool isZone267)
    {
        if (topThree.IsDefaultOrEmpty)
            return [];

        var (top1, top2, top3) = isFfaMap
            ? (KillFeedRewardConstants.FfaTop1ContributionPoints, KillFeedRewardConstants.FfaTop2ContributionPoints,
                KillFeedRewardConstants.FfaTop3ContributionPoints)
            : (KillFeedRewardConstants.NonFfaTop1ContributionPoints,
                KillFeedRewardConstants.NonFfaTop2ContributionPoints,
                KillFeedRewardConstants.NonFfaTop3ContributionPoints);

        if (!isFfaMap && isZone267)
        {
            top1 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
            top2 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
            top3 *= KillFeedRewardConstants.Zone267ContributionPointMultiplier;
        }

        ReadOnlySpan<int> amountByRank = [top1, top2, top3];

        var count = Math.Min(3, topThree.Length);
        var builder = ImmutableArray.CreateBuilder<RankReward>(count);
        for (var i = 0; i < count; i++)
            builder.Add(new RankReward(topThree[i].CharacterId, amountByRank[i]));

        return builder.MoveToImmutable();
    }

        public readonly record struct RankReward(int CharacterId, int ContributionPoints);
}
