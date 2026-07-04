using Fenrir.Application.Game.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>Covers <see cref="ExperienceFormulas" /> against <c>MONSTER_OBJECT::ProcessForExp</c> and the death XP loss path in <c>Server/ts25zone/S07_MyGame05.cpp</c> / <c>S07_MyGame02.cpp</c>.</summary>
public class ExperienceFormulasTests
{
    [Theory]
    [InlineData(50, 50)] // < 100 -> pass-through
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
        // killerFixedLevel(60) - monsterRealLevel(50) = 10 > 9 -> refused.
        Assert.Equal(0, ExperienceFormulas.ComputeMonsterKillExperience(60, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_EqualLevels_GrantsFullBase()
    {
        // Neither branch's gap term applies (killer <= monster, gap=0): gain = base * (1 + 0 - 0) = base.
        Assert.Equal(1000, ExperienceFormulas.ComputeMonsterKillExperience(50, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_FavorableGapWithinTwenty_ScalesUpTenPercentPerLevel()
    {
        // monster(55) > killer(50): gap=5 -> gain = 1000 * (1 + 5*0.1) = 1500.
        Assert.Equal(1500, ExperienceFormulas.ComputeMonsterKillExperience(50, 55, 1000));
    }

    [Fact]
    public void MonsterKillExperience_FavorableGapOverTwenty_Triples()
    {
        // monster(80) > killer(50): gap=30 > 20 -> gain = 1000 * 3 = 3000.
        Assert.Equal(3000, ExperienceFormulas.ComputeMonsterKillExperience(50, 80, 1000));
    }

    [Fact]
    public void MonsterKillExperience_UnfavorableGapWithinNine_ScalesDownTenPercentPerLevel()
    {
        // killer(55) > monster(50): gap=5 -> gain = 1000 * (1 - 5*0.1) = 500.
        Assert.Equal(500, ExperienceFormulas.ComputeMonsterKillExperience(55, 50, 1000));
    }

    [Fact]
    public void MonsterKillExperience_UnfavorableGapNeverGoesNegative()
    {
        // 9*0.1f floats a hair under 0.9f, so truncation gives 99 not 100 -- matches legacy C++ float math
        Assert.Equal(99, ExperienceFormulas.ComputeMonsterKillExperience(59, 50, 1000));
    }

    [Theory]
    [InlineData(112, 3)] // below LV_M1(113) -> divide by 3
    [InlineData(113, 5)] // at/above LV_M1 -> divide by 5
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
        // (10000 - 2000) * 0.05 = 400.
        Assert.Equal(400, ExperienceFormulas.ComputeDeathExperienceLoss(10000, 2000));
    }

    [Fact]
    public void DeathExperienceLoss_BelowOne_RoundsDownToZero()
    {
        Assert.Equal(0, ExperienceFormulas.ComputeDeathExperienceLoss(2010, 2000)); // (2010-2000)*0.05=0.5 -> 0
    }

    [Fact]
    public void DeathExperienceLoss_NeverExceedsCurrentExperience()
    {
        // clamped to currentExperience itself, never negative
        Assert.Equal(100, ExperienceFormulas.ComputeDeathExperienceLoss(100, -100_000));
    }

    [Theory]
    [InlineData(2, 100)] // +10%
    [InlineData(3, 200)] // +20%
    [InlineData(4, 300)] // +30%
    [InlineData(5, 500)] // +50%
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
