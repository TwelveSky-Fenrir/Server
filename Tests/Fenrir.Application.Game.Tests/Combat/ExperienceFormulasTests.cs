using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class ExperienceFormulasTests
{
    [Theory]
    [InlineData(50, 50)]
    [InlineData(99, 99)]
    [InlineData(100, 102)]
    [InlineData(113, 143)]
    [InlineData(145, 335)]
    [InlineData(157, 815)]
    public void ReturnFixedLevel_MatchesTheVerifiedTable(int level, int expected)
    {
        Assert.Equal(expected, ExperienceFormulas.ReturnFixedLevel(level));
    }

    [Fact]
    public void ReturnFixedLevel_OutsideTheTable_ReturnsOne()
    {
        Assert.Equal(1, ExperienceFormulas.ReturnFixedLevel(158));
        Assert.Equal(1, ExperienceFormulas.ReturnFixedLevel(9999));
    }

    [Fact]
    public void MonsterKillExperience_ZeroGeneralExperience_GrantsNothing()
    {
        Assert.Equal(0, ExperienceFormulas.ComputeMonsterKillExperience(50, 50, 0));
    }

    [Fact]
    public void MonsterKillExperience_UnfavorableGapOverNine_GrantsNothing()
    {
        Assert.Equal(0, ExperienceFormulas.ComputeMonsterKillExperience(60, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_EqualLevels_GrantsFullBase()
    {
        Assert.Equal(1000, ExperienceFormulas.ComputeMonsterKillExperience(50, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_FavorableGapWithinTwenty_ScalesUpTenPercentPerLevel()
    {
        Assert.Equal(1500, ExperienceFormulas.ComputeMonsterKillExperience(50, 55, 1000));
    }

    [Fact]
    public void MonsterKillExperience_FavorableGapOverTwenty_Triples()
    {
        Assert.Equal(3000, ExperienceFormulas.ComputeMonsterKillExperience(50, 80, 1000));
    }

    [Fact]
    public void MonsterKillExperience_UnfavorableGapWithinNine_ScalesDownTenPercentPerLevel()
    {
        Assert.Equal(500, ExperienceFormulas.ComputeMonsterKillExperience(55, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_UnfavorableGapNeverGoesNegative()
    {
        Assert.Equal(99, ExperienceFormulas.ComputeMonsterKillExperience(59, 50, 1000));
    }

    [Theory]
    [InlineData(112, 3)]
    [InlineData(113, 5)]
    [InlineData(200, 5)]
    public void ApplyRebirthDivisor_SplitsOnLvM1(int characterLevel, int divisor)
    {
        Assert.Equal(3000 / divisor, ExperienceFormulas.ApplyRebirthDivisor(3000, characterLevel));
    }

    [Fact]
    public void ApplyRebirthDivisor_NonPositiveGain_StaysZero()
    {
        Assert.Equal(0, ExperienceFormulas.ApplyRebirthDivisor(0, 50));
        Assert.Equal(0, ExperienceFormulas.ApplyRebirthDivisor(-100, 50));
    }

    [Fact]
    public void DeathExperienceLoss_IsFivePercentOfExperienceAboveTheLevelFloor()
    {
        Assert.Equal(400, ExperienceFormulas.ComputeDeathExperienceLoss(10000, 2000));
    }

    [Fact]
    public void DeathExperienceLoss_BelowOne_RoundsDownToZero()
    {
        Assert.Equal(0, ExperienceFormulas.ComputeDeathExperienceLoss(2010, 2000));
    }

    [Fact]
    public void DeathExperienceLoss_NeverExceedsCurrentExperience()
    {
        Assert.Equal(100, ExperienceFormulas.ComputeDeathExperienceLoss(100, -100_000));
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(3, 200)]
    [InlineData(4, 300)]
    [InlineData(5, 500)]
    public void ComputePartyBonusExperience_MatchesTheVerifiedSwitchTable(int presentPartySize, int expectedBonus)
    {
        Assert.Equal(expectedBonus, ExperienceFormulas.ComputePartyBonusExperience(presentPartySize, 1000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    public void ComputePartyBonusExperience_SizeOutsideTwoToFive_GrantsNothing(int presentPartySize)
    {
        Assert.Equal(0, ExperienceFormulas.ComputePartyBonusExperience(presentPartySize, 1000));
    }

    [Fact]
    public void ComputePartyBonusExperience_ZeroGeneralExperience_GrantsNothing()
    {
        Assert.Equal(0, ExperienceFormulas.ComputePartyBonusExperience(3, 0));
    }
}
