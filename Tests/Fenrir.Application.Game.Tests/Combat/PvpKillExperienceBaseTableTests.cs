using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillExperienceBaseTableTests
{
    [Theory]
    [InlineData(1, PvpKillExperienceBaseTable.LowTierBase)]
    [InlineData(2, PvpKillExperienceBaseTable.LowTierBase)]
    [InlineData(100, PvpKillExperienceBaseTable.LowTierBase)]
    [InlineData(145, PvpKillExperienceBaseTable.LowTierBase)]
    public void LowTier_EveryCombinedLevelOneToOneHundredFortyFive_Yields110(int level, int expected)
    {
        Assert.Equal(expected, PvpKillExperienceBaseTable.Lookup(level));
    }

    [Theory]
    [InlineData(146)]
    [InlineData(147)]
    [InlineData(148)]
    [InlineData(149)]
    public void HighTierOne_LevelsOneFortySixToOneFortyNine_Yields330(int level)
    {
        Assert.Equal(PvpKillExperienceBaseTable.HighTier1Base, PvpKillExperienceBaseTable.Lookup(level));
    }

    [Theory]
    [InlineData(150)]
    [InlineData(151)]
    [InlineData(152)]
    [InlineData(153)]
    public void HighTierTwo_LevelsOneFiftyToOneFiftyThree_Yields360(int level)
    {
        Assert.Equal(PvpKillExperienceBaseTable.HighTier2Base, PvpKillExperienceBaseTable.Lookup(level));
    }

    [Theory]
    [InlineData(154)]
    [InlineData(155)]
    [InlineData(156)]
    [InlineData(157)]
    public void HighTierThree_LevelsOneFiftyFourToOneFiftySeven_Yields390(int level)
    {
        Assert.Equal(PvpKillExperienceBaseTable.HighTier3Base, PvpKillExperienceBaseTable.Lookup(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(158)]
    [InlineData(1000)]
    public void OutsideOneToOneFiftySeven_YieldsZero(int level)
    {
        Assert.Equal(0, PvpKillExperienceBaseTable.Lookup(level));
    }

    [Fact]
    public void TierBoundaries_FlipExactlyAtTheDocumentedLevels()
    {
        Assert.Equal(PvpKillExperienceBaseTable.LowTierBase, PvpKillExperienceBaseTable.Lookup(145));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier1Base, PvpKillExperienceBaseTable.Lookup(146));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier1Base, PvpKillExperienceBaseTable.Lookup(149));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier2Base, PvpKillExperienceBaseTable.Lookup(150));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier2Base, PvpKillExperienceBaseTable.Lookup(153));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier3Base, PvpKillExperienceBaseTable.Lookup(154));
        Assert.Equal(PvpKillExperienceBaseTable.HighTier3Base, PvpKillExperienceBaseTable.Lookup(157));
    }
}
