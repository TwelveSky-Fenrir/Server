using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedEndOfBattleRewardCalculator
{
    public static ImmutableArray<RankReward> ComputeRankRewards(ImmutableArray<KillFeedRankedEntry> topThree,
        bool isZone267)
    {
        if (topThree.IsDefaultOrEmpty)
            return [];

        var multiplier = isZone267 ? KillFeedRewardConstants.Zone267ContributionPointMultiplier : 1;

        ReadOnlySpan<int> amountByRank =
        [
            KillFeedRewardConstants.Top1ContributionPoints * multiplier,
            KillFeedRewardConstants.Top2ContributionPoints * multiplier,
            KillFeedRewardConstants.Top3ContributionPoints * multiplier
        ];

        var count = Math.Min(3, topThree.Length);
        var builder = ImmutableArray.CreateBuilder<RankReward>(count);
        for (var i = 0; i < count; i++)
            builder.Add(new RankReward(topThree[i].CharacterId, amountByRank[i]));

        return builder.MoveToImmutable();
    }

    public readonly record struct RankReward(int CharacterId, int ContributionPoints);
}
