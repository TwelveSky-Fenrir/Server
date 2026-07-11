using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class FavoredTribeRankBonusLadderTests
{
    [Fact]
    public void FavoredTribe0_GetsBaselinePlusFlatBonus_OthersGetDistanceTiers()
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(0);

        Assert.Equal(1000 + 4000, totals[0]);
        Assert.Equal(1000 + 100, totals[1]);
        Assert.Equal(1000 + 200, totals[2]);
        Assert.Equal(1000 + 300, totals[3]);
    }

    [Fact]
    public void FavoredTribe1_DistancesWrapCyclically()
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(1);

        Assert.Equal(1000 + 300, totals[0]);
        Assert.Equal(1000 + 4000, totals[1]);
        Assert.Equal(1000 + 100, totals[2]);
        Assert.Equal(1000 + 200, totals[3]);
    }

    [Fact]
    public void FavoredTribe2_DistancesWrapCyclically()
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(2);

        Assert.Equal(1000 + 200, totals[0]);
        Assert.Equal(1000 + 300, totals[1]);
        Assert.Equal(1000 + 4000, totals[2]);
        Assert.Equal(1000 + 100, totals[3]);
    }

    [Fact]
    public void FavoredTribe3_DistancesWrapCyclically()
    {
        var totals = FavoredTribeRankBonusLadder.ComputeTotals(3);

        Assert.Equal(1000 + 100, totals[0]);
        Assert.Equal(1000 + 200, totals[1]);
        Assert.Equal(1000 + 300, totals[2]);
        Assert.Equal(1000 + 4000, totals[3]);
    }

    [Fact]
    public void EveryTotal_IsFullyDefined_NoTribeIsEverLeftAtZero()
    {
        for (byte favored = 0; favored < 4; favored++)
        {
            var totals = FavoredTribeRankBonusLadder.ComputeTotals(favored);
            Assert.All(totals, t => Assert.True(t >= 1000));
        }
    }

    [Fact]
    public void OutOfRangeFavoredTribe_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FavoredTribeRankBonusLadder.ComputeTotals(4));
    }
}
