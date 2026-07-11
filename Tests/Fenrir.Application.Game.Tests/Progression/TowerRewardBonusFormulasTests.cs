using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerRewardBonusFormulasTests
{
    [Theory]
    [InlineData(1, 0.05f)]
    [InlineData(2, 0.10f)]
    [InlineData(3, 0.15f)]
    [InlineData(4, 0.20f)]
    public void SilverBonusRatio_MatchesTheLegacyPerLevelTable(int builtLevel, float expected)
    {
        Assert.Equal(expected, TowerRewardBonusFormulas.SilverBonusRatio(builtLevel));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    public void CpForPvmBonus_CapsAtTwoFromLevelTwoOnward(int builtLevel, int expected)
    {
        Assert.Equal(expected, TowerRewardBonusFormulas.CpForPvmBonus(builtLevel));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void CpForPvpBonus_OnlyStartsContributingAtLevelThree(int builtLevel, int expected)
    {
        Assert.Equal(expected, TowerRewardBonusFormulas.CpForPvpBonus(builtLevel));
    }

    [Theory]
    [InlineData(1, 0.25f)]
    [InlineData(2, 0.50f)]
    [InlineData(3, 0.75f)]
    [InlineData(4, 1.00f)]
    public void XpBonusRatio_MatchesTheLegacyPerLevelTable(int builtLevel, float expected)
    {
        Assert.Equal(expected, TowerRewardBonusFormulas.XpBonusRatio(builtLevel));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void EveryFormula_OutsideOneToFour_YieldsZero(int builtLevel)
    {
        Assert.Equal(0f, TowerRewardBonusFormulas.SilverBonusRatio(builtLevel));
        Assert.Equal(0, TowerRewardBonusFormulas.CpForPvmBonus(builtLevel));
        Assert.Equal(0, TowerRewardBonusFormulas.CpForPvpBonus(builtLevel));
        Assert.Equal(0f, TowerRewardBonusFormulas.XpBonusRatio(builtLevel));
    }

        [Fact]
    public void LevelFourCpTower_YieldsBothPvmAndPvpBonusesSimultaneously()
    {
        Assert.Equal(2, TowerRewardBonusFormulas.CpForPvmBonus(4));
        Assert.Equal(2, TowerRewardBonusFormulas.CpForPvpBonus(4));
    }
}

public class TowerRewardBonusTableTests
{
    private static int[] EmptyTowers()
    {
        return new int[TowerWarState.TowerCount];
    }

    [Fact]
    public void AllTowersUnbuilt_EveryTribeGetsAllZeroBonus()
    {
        var bonuses = TowerRewardBonusTable.Recompute(EmptyTowers());

        Assert.Equal(TowerRewardBonusTable.TribeCount, bonuses.Length);
        Assert.All(bonuses, b => Assert.Equal(TowerTribeRewardBonus.None, b));
    }

    [Fact]
    public void SingleSilverTowerAtLevelTwo_OnlySetsThatTribesSilverRatio()
    {
        var towers = EmptyTowers();
        towers[0] = 4 * 100 + 1;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.10f, bonuses[0].SilverRatio);
        Assert.Equal(0, bonuses[0].CpForPvmBonus);
        Assert.Equal(0, bonuses[0].CpForPvpBonus);
        Assert.Equal(0f, bonuses[0].XpRatio);
        Assert.Equal(TowerTribeRewardBonus.None, bonuses[1]);
        Assert.Equal(TowerTribeRewardBonus.None, bonuses[2]);
        Assert.Equal(TowerTribeRewardBonus.None, bonuses[3]);
    }

    [Fact]
    public void CpTowerAtLevelFour_SetsBothPvmAndPvpFieldsForItsTribe()
    {
        var towers = EmptyTowers();
        towers[3] = 8 * 100 + 2;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(2, bonuses[1].CpForPvmBonus);
        Assert.Equal(2, bonuses[1].CpForPvpBonus);
        Assert.Equal(0f, bonuses[1].SilverRatio);
        Assert.Equal(0f, bonuses[1].XpRatio);
    }

    [Fact]
    public void XpTowerAtLevelThree_SetsOnlyXpRatioForItsTribe()
    {
        var towers = EmptyTowers();
        towers[9] = 6 * 100 + 3;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.75f, bonuses[3].XpRatio);
        Assert.Equal(0, bonuses[3].CpForPvmBonus);
        Assert.Equal(0, bonuses[3].CpForPvpBonus);
    }

        [Fact]
    public void TwoSameTypeTowersInOneTribesGroup_HighestSlotLocalIndexWins()
    {
        var towers = EmptyTowers();
        towers[0] = 2 * 100 + 1;
        towers[2] = 8 * 100 + 1;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.20f, bonuses[0].SilverRatio);
    }

    [Fact]
    public void TwoSameTypeTowers_LowerIndexProcessedLastAmongThem_StillLosesToHigherLocalIndex()
    {
        var towers = EmptyTowers();
        towers[1] = 8 * 100 + 1;
        towers[2] = 2 * 100 + 1;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.05f, bonuses[0].SilverRatio);
    }

    [Fact]
    public void UnbuiltStateCode1_CountsAsLevelZero_NoBonus()
    {
        var towers = EmptyTowers();
        towers[0] = 1 * 100 + 1;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(TowerTribeRewardBonus.None, bonuses[0]);
    }

    [Fact]
    public void AllFourTribesBuiltSimultaneously_EachGetsItsOwnIndependentBonus()
    {
        var towers = EmptyTowers();
        towers[0] = 2 * 100 + 1;
        towers[3] = 4 * 100 + 2;
        towers[6] = 6 * 100 + 3;
        towers[9] = 8 * 100 + 1;

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.05f, bonuses[0].SilverRatio);
        Assert.Equal(2, bonuses[1].CpForPvmBonus);
        Assert.Equal(0, bonuses[1].CpForPvpBonus);
        Assert.Equal(0.75f, bonuses[2].XpRatio);
        Assert.Equal(0.20f, bonuses[3].SilverRatio);
    }
}
