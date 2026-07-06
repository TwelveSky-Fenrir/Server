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

    /// <summary>A level-4 CP tower is the verified non-monotonic case: +2 PvM and +2 PvP simultaneously, not +2 total.</summary>
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
        towers[0] = 4 * 100 + 1; // tribe 0, slot-local 0: built level 2 (raw digit 4), type 1 (Silver)

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
        towers[3] = 8 * 100 + 2; // tribe 1, slot-local 0 (tower index 3): built level 4 (raw digit 8), type 2 (CP)

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
        towers[9] = 6 * 100 + 3; // tribe 3, slot-local 0 (tower index 9): built level 3 (raw digit 6), type 3 (XP)

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.75f, bonuses[3].XpRatio);
        Assert.Equal(0, bonuses[3].CpForPvmBonus);
        Assert.Equal(0, bonuses[3].CpForPvpBonus);
    }

    /// <summary>
    ///     Overwrite, not additive/max: two Silver towers in the same tribe's own group -- the higher slot-local
    ///     index (processed last) wins.
    /// </summary>
    [Fact]
    public void TwoSameTypeTowersInOneTribesGroup_HighestSlotLocalIndexWins()
    {
        var towers = EmptyTowers();
        towers[0] = 2 * 100 + 1; // tribe 0 slot-local 0: level 1 Silver
        towers[2] = 8 * 100 + 1; // tribe 0 slot-local 2: level 4 Silver -- processed last, must win

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.20f, bonuses[0].SilverRatio);
    }

    [Fact]
    public void TwoSameTypeTowers_LowerIndexProcessedLastAmongThem_StillLosesToHigherLocalIndex()
    {
        var towers = EmptyTowers();
        towers[1] = 8 * 100 + 1; // slot-local 1: level 4 Silver
        towers[2] = 2 * 100 + 1; // slot-local 2: level 1 Silver -- processed last, must win despite being weaker

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.05f, bonuses[0].SilverRatio);
    }

    [Fact]
    public void UnbuiltStateCode1_CountsAsLevelZero_NoBonus()
    {
        var towers = EmptyTowers();
        towers[0] = 1 * 100 + 1; // raw state 1 ("creating, still cooling down") -- must NOT count as built

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(TowerTribeRewardBonus.None, bonuses[0]);
    }

    [Fact]
    public void AllFourTribesBuiltSimultaneously_EachGetsItsOwnIndependentBonus()
    {
        var towers = EmptyTowers();
        towers[0] = 2 * 100 + 1; // tribe 0: Silver L1
        towers[3] = 4 * 100 + 2; // tribe 1: CP L2
        towers[6] = 6 * 100 + 3; // tribe 2: XP L3
        towers[9] = 8 * 100 + 1; // tribe 3: Silver L4

        var bonuses = TowerRewardBonusTable.Recompute(towers);

        Assert.Equal(0.05f, bonuses[0].SilverRatio);
        Assert.Equal(2, bonuses[1].CpForPvmBonus);
        Assert.Equal(0, bonuses[1].CpForPvpBonus);
        Assert.Equal(0.75f, bonuses[2].XpRatio);
        Assert.Equal(0.20f, bonuses[3].SilverRatio);
    }
}
