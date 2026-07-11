using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class KillFeedEndOfBattleRewardCalculatorTests
{
    private static ImmutableArray<KillFeedRankedEntry> ThreeEntries()
    {
        return
        [
            new KillFeedRankedEntry(1, "First", 0, 9),
            new KillFeedRankedEntry(2, "Second", 1, 5),
            new KillFeedRankedEntry(3, "Third", 2, 1)
        ];
    }

    [Fact]
    public void EmptyLeaderboard_NoRewards()
    {
        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards([], false, false);

        Assert.Empty(rewards);
    }

    [Fact]
    public void FfaMap_GrantsFfaCpTable()
    {
        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(ThreeEntries(), true, false);

        Assert.Equal(3, rewards.Length);
        Assert.Equal(1, rewards[0].CharacterId);
        Assert.Equal(KillFeedRewardConstants.FfaTop1ContributionPoints, rewards[0].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.FfaTop2ContributionPoints, rewards[1].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.FfaTop3ContributionPoints, rewards[2].ContributionPoints);
    }

    [Fact]
    public void NonFfaMap_NotZone267_GrantsBaseCpTable()
    {
        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(ThreeEntries(), false, false);

        Assert.Equal(KillFeedRewardConstants.NonFfaTop1ContributionPoints, rewards[0].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.NonFfaTop2ContributionPoints, rewards[1].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.NonFfaTop3ContributionPoints, rewards[2].ContributionPoints);
    }

    [Fact]
    public void Zone267_DoublesTheBaseCpTable()
    {
        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(ThreeEntries(), false, true);

        Assert.Equal(KillFeedRewardConstants.NonFfaTop1ContributionPoints * 2, rewards[0].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.NonFfaTop2ContributionPoints * 2, rewards[1].ContributionPoints);
        Assert.Equal(KillFeedRewardConstants.NonFfaTop3ContributionPoints * 2, rewards[2].ContributionPoints);
    }

    [Fact]
    public void FewerThanThreeEntries_OnlyRewardsThoseTracked()
    {
        ImmutableArray<KillFeedRankedEntry> oneEntry = [new KillFeedRankedEntry(7, "Solo", 0, 4)];

        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(oneEntry, true, false);

        Assert.Single(rewards);
        Assert.Equal(7, rewards[0].CharacterId);
        Assert.Equal(KillFeedRewardConstants.FfaTop1ContributionPoints, rewards[0].ContributionPoints);
    }

    [Fact]
    public void EveryRankedCharacterIsDistinct_NoPlayerCreditedTwice()
    {
        var rewards = KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(ThreeEntries(), true, false);

        Assert.Equal(rewards.Length, rewards.Select(r => r.CharacterId).Distinct().Count());
    }
}
