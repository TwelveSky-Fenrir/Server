using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarRewardCalculatorTests
{
    private static RegularWarMapConfig OrdinaryMap()
    {
        Assert.True(RegularWarMapCatalog.TryGet(49, out var config));
        return config;
    }

    private static RegularWarMapConfig Server120()
    {
        Assert.True(RegularWarMapCatalog.TryGet(120, out var config));
        return config;
    }

    private static RegularWarMapConfig Server164()
    {
        Assert.True(RegularWarMapCatalog.TryGet(164, out var config));
        return config;
    }

    [Fact]
    public void AbortedEmptyMap_NeverGrantsAnything()
    {
        var participants = new[] { new RegularWarParticipant(1, 0, 100, 5, 2) };

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.AbortedEmptyMap, null,
            null, OrdinaryMap(), participants, [], new FakeRewardValueProvider(1000, 100));

        Assert.Empty(grants);
    }

    [Fact]
    public void Draw_GrantsHalvedMoneyAndExperience_NoCpBonusNoItemDrop_LosingRateHeroPoints()
    {
        var participants = new[]
        {
            new RegularWarParticipant(1, 0, 100, 5, 2),
            new RegularWarParticipant(2, 2, 100, 5, 2)
        };

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.Draw, null,
            null, OrdinaryMap(), participants, [], new FakeRewardValueProvider(1000, 100));

        Assert.All(grants, grant =>
        {
            Assert.Null(grant.IsWinningSide);
            Assert.Equal(500, grant.MoneyAmount);
            Assert.Equal(50, grant.ExperienceAmount);
            Assert.Equal(0, grant.CpBonusAmount);
            Assert.Equal(RegularWarRewardCalculator.LosingOrDrawHeroRankPoints, grant.HeroRankPoints);
            Assert.False(grant.RequestItemDrop);
            Assert.True(grant.GrantParticipationCounter);
        });
    }

    [Fact]
    public void Draw_StillPaysLeaderboardCp_ToTheTopThreeKillers()
    {
        var participants = new[]
        {
            new RegularWarParticipant(1, 0, 100, 5, 2),
            new RegularWarParticipant(2, 2, 100, 5, 2),
            new RegularWarParticipant(3, 1, 100, 5, 2)
        };

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.Draw, null,
            null, OrdinaryMap(), participants, [1, 2, 3], new FakeRewardValueProvider(1000, 100));

        Assert.Equal(100, grants.Single(g => g.CharacterId == 1).LeaderboardCpAmount);
        Assert.Equal(50, grants.Single(g => g.CharacterId == 2).LeaderboardCpAmount);
        Assert.Equal(25, grants.Single(g => g.CharacterId == 3).LeaderboardCpAmount);
    }

    [Fact]
    public void TribeWin_WinningSide_GetsFullMoneyAndExperience_LosingSideGetsExactlyHalf_RemainderLost()
    {
        var winner = new RegularWarParticipant(1, 0, 100, 5, 2);
        var loser = new RegularWarParticipant(2, 2, 100, 5, 2);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, OrdinaryMap(), [winner, loser], [], new FakeRewardValueProvider(11, 7));

        var winnerGrant = grants.Single(g => g.CharacterId == 1);
        var loserGrant = grants.Single(g => g.CharacterId == 2);

        Assert.True(winnerGrant.IsWinningSide);
        Assert.Equal(11, winnerGrant.MoneyAmount);
        Assert.Equal(7, winnerGrant.ExperienceAmount);
        Assert.Equal(RegularWarRewardCalculator.WinningHeroRankPoints, winnerGrant.HeroRankPoints);
        Assert.True(winnerGrant.RequestItemDrop);

        Assert.False(loserGrant.IsWinningSide);
        Assert.Equal(5, loserGrant.MoneyAmount);
        Assert.Equal(3, loserGrant.ExperienceAmount);
        Assert.Equal(RegularWarRewardCalculator.LosingOrDrawHeroRankPoints, loserGrant.HeroRankPoints);
        Assert.True(loserGrant.RequestItemDrop);
    }

    [Fact]
    public void TribeWin_AllyOfWinningTribe_CountsAsWinningSide()
    {
        var allyMember = new RegularWarParticipant(1, 1, 100, 5, 2);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            1, OrdinaryMap(), [allyMember], [], new FakeRewardValueProvider(1000, 100));

        Assert.True(Assert.Single(grants).IsWinningSide);
    }

    [Fact]
    public void TribeWin_NonPositiveBaseAmounts_GrantNothing()
    {
        var winner = new RegularWarParticipant(1, 0, 100, 0, 0);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, OrdinaryMap(), [winner], [], new FakeRewardValueProvider(0, 0));

        var grant = Assert.Single(grants);
        Assert.Equal(0, grant.MoneyAmount);
        Assert.Equal(0, grant.ExperienceAmount);
    }

    [Theory]
    [InlineData((short)11, 0, true)]
    [InlineData((short)10, 0, false)]
    [InlineData((short)12, 0, false)]
    public void Server120_CpBonus_RestrictedToExactlyRebirthTierEleven(short rebirthTier, int rebirthCount,
        bool shouldQualify)
    {
        var winner = new RegularWarParticipant(1, 0, 100, rebirthTier, rebirthCount);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, Server120(), [winner], [], new FakeRewardValueProvider(1000, 100));

        var grant = Assert.Single(grants);
        Assert.Equal(shouldQualify ? 50 : 0, grant.CpBonusAmount);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void Server164_CpBonus_RestrictedToRebirthCountZeroToSix(int rebirthCount, bool shouldQualify)
    {
        var loser = new RegularWarParticipant(1, 3, 100, 5, rebirthCount);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, Server164(), [loser], [], new FakeRewardValueProvider(1000, 100));

        var grant = Assert.Single(grants);
        Assert.Equal(shouldQualify ? 50 : 0, grant.CpBonusAmount);
    }

    [Fact]
    public void OrdinaryMap_NeverGrantsACpBonus_RegardlessOfRebirthTierOrCount()
    {
        var winner = new RegularWarParticipant(1, 0, 100, 11, 3);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, OrdinaryMap(), [winner], [], new FakeRewardValueProvider(1000, 100));

        Assert.Equal(0, Assert.Single(grants).CpBonusAmount);
    }

    [Fact]
    public void LeaderboardCp_AppliesRegardlessOfTribeOrOutcome_OnTopOfEveryOtherGrant()
    {
        var winner = new RegularWarParticipant(1, 0, 100, 5, 2);
        var loserWhoTopFragged = new RegularWarParticipant(2, 2, 100, 5, 2);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, OrdinaryMap(), [winner, loserWhoTopFragged], [2],
            new FakeRewardValueProvider(1000, 100));

        var loserGrant = grants.Single(g => g.CharacterId == 2);
        Assert.False(loserGrant.IsWinningSide);
        Assert.Equal(100, loserGrant.LeaderboardCpAmount);
        Assert.Equal(500, loserGrant.MoneyAmount);
    }

    [Fact]
    public void LeaderboardCp_ACharacterNotInTheTopThree_GetsZero()
    {
        var participant = new RegularWarParticipant(1, 0, 100, 5, 2);

        var grants = RegularWarRewardCalculator.Compute(RegularWarOutcome.TribeWin, 0,
            null, OrdinaryMap(), [participant], [99, 98, 97], new FakeRewardValueProvider(1000, 100));

        Assert.Equal(0, Assert.Single(grants).LeaderboardCpAmount);
    }

    private sealed class FakeRewardValueProvider(long money, int experience) : IRegularWarRewardValueProvider
    {
        public long GetMoneyReward(short evolutionTier, short level)
        {
            return evolutionTier > 0 ? money : 0;
        }

        public int GetExperienceReward(short level)
        {
            return experience;
        }
    }
}
