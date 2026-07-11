using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class HeroRewardResolverTests
{
    private static HeroRankingRowDto Row(int characterId, byte? tribe, int points, bool? claimed = null)
    {
        return new HeroRankingRowDto(characterId, $"P{characterId}", tribe, points, null, claimed, null,
            DateTime.UnixEpoch);
    }

    [Fact]
    public void CharacterNotInStandings_IsNotRanked()
    {
        var rows = new[] { Row(1, 0, 500), Row(2, 0, 400) };

        var result = HeroRewardResolver.Resolve(rows, 0, 999);

        Assert.Equal(HeroRewardResolver.Outcome.NotRanked, result.Outcome);
    }

    [Fact]
    public void RankedInDifferentTribeThanCaller_IsNotRanked()
    {
        var rows = new[] { Row(1, 1, 500) };

        var result = HeroRewardResolver.Resolve(rows, 0, 1);

        Assert.Equal(HeroRewardResolver.Outcome.NotRanked, result.Outcome);
    }

    [Fact]
    public void RankedFirstInOwnTribe_ClaimsTopCpAmount()
    {
        var rows = new[] { Row(1, 0, 500), Row(2, 0, 400), Row(3, 1, 999) };

        var result = HeroRewardResolver.Resolve(rows, 0, 1);

        Assert.Equal(HeroRewardResolver.Outcome.Claim, result.Outcome);
        Assert.Equal(0, result.Rank);
        Assert.Equal(1000, HeroRewardResolver.PointsByRank[result.Rank]);
        Assert.Equal(1, result.Row!.CharacterId);
    }

    [Fact]
    public void RankedTenthInOwnTribe_ClaimsLowestCpAmount()
    {
        var rows = Enumerable.Range(0, 10).Select(i => Row(100 + i, 2, 1000 - i)).ToArray();

        var result = HeroRewardResolver.Resolve(rows, 2, 109);

        Assert.Equal(HeroRewardResolver.Outcome.Claim, result.Outcome);
        Assert.Equal(9, result.Rank);
        Assert.Equal(100, HeroRewardResolver.PointsByRank[result.Rank]);
    }

    [Fact]
    public void RankedEleventhInOwnTribe_FallsOutsideTop10_IsNotRanked()
    {
        var rows = Enumerable.Range(0, 11).Select(i => Row(100 + i, 2, 1000 - i)).ToArray();

        var result = HeroRewardResolver.Resolve(rows, 2, 110);

        Assert.Equal(HeroRewardResolver.Outcome.NotRanked, result.Outcome);
    }

    [Fact]
    public void AlreadyClaimedRow_ReportsAlreadyClaimed()
    {
        var rows = new[] { Row(1, 0, 500, true) };

        var result = HeroRewardResolver.Resolve(rows, 0, 1);

        Assert.Equal(HeroRewardResolver.Outcome.AlreadyClaimed, result.Outcome);
    }

    // Resolve now goes through HeroRankAcceptStateRules.FromClaimedFlag/IsClaimable instead of a bare
    // `row.RewardClaimed == true` comparison -- these two cases pin that the tri-state mapping still treats
    // an explicit false exactly like a null (both Unclaimed/Claim), not just the null case the other tests
    // above already exercise via the Row helper's own `claimed = null` default.
    [Fact]
    public void ExplicitlyFalseRewardClaimed_IsStillClaimable()
    {
        var rows = new[] { Row(1, 0, 500, false) };

        var result = HeroRewardResolver.Resolve(rows, 0, 1);

        Assert.Equal(HeroRewardResolver.Outcome.Claim, result.Outcome);
    }

    [Fact]
    public void NullRewardClaimed_IsClaimable()
    {
        var rows = new[] { Row(1, 0, 500) };

        var result = HeroRewardResolver.Resolve(rows, 0, 1);

        Assert.Equal(HeroRewardResolver.Outcome.Claim, result.Outcome);
    }
}
