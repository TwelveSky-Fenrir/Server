using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillExperienceCalculatorTests
{
    [Fact]
    public void EqualLevels_GrantsBaseAmountUnscaled()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: false);

        Assert.Equal(100, gain);
    }

    [Fact]
    public void UnfavorableGapAtThreshold_StillGrants()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 59, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: false);

        Assert.Equal(100, gain);
    }

    [Fact]
    public void UnfavorableGapPastThreshold_ZeroesOutEntirely()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 60, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: false);

        Assert.Equal(0, gain);
    }

    [Fact]
    public void FavorableGap_DoesNotZeroOut()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 10, defenderCombinedLevel: 80,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: false);

        Assert.Equal(100, gain);
    }

    [Fact]
    public void ZeroBaseAmount_ReturnsZero()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(0, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: true, hasDoubleExpCharge: true);

        Assert.Equal(0, gain);
    }

    [Fact]
    public void ZoneMultiplier_ScalesBaseAmount()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: false, zoneMultiplier: 2.0f);

        Assert.Equal(200, gain);
    }

    [Fact]
    public void WarriorScrollBuff_DoublesGain()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: true, hasDoubleExpCharge: false);

        Assert.Equal(200, gain);
    }

    [Fact]
    public void DoubleExpCharge_MultipliesGainByEight()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: false, hasDoubleExpCharge: true);

        Assert.Equal(800, gain);
    }

    [Fact]
    public void WarriorScrollAndDoubleExpCharge_StackMultiplicatively()
    {
        var gain = PvpKillExperienceCalculator.ComputeGain(100, attackerCombinedLevel: 50, defenderCombinedLevel: 50,
            hasWarriorScrollBuff: true, hasDoubleExpCharge: true);

        Assert.Equal(1600, gain);
    }
}
