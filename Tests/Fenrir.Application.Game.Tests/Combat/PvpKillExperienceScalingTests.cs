using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillExperienceScalingTests
{
    [Fact]
    public void EqualCombinedLevels_ReturnsBaseUnchanged()
    {
        // gap 0 -> down branch with a zero penalty: base minus (base * 0) == base, exactly.
        Assert.Equal(110, PvpKillExperienceScaling.Scale(110, 100, 100));
    }

    [Fact]
    public void AttackerBelowVictim_UpScalesTenPercentPerLevel()
    {
        // attacker 97 vs victim 100 -> favorable gap 3 -> 110 + 110 * 0.3 == 143.
        Assert.Equal(143, PvpKillExperienceScaling.Scale(110, 97, 100));
    }

    [Fact]
    public void AttackerOneLevelAboveVictim_DownScalesTenPercent()
    {
        // attacker 101 vs victim 100 -> unfavorable gap 1 -> 110 - 110 * 0.1 == 99.
        Assert.Equal(99, PvpKillExperienceScaling.Scale(110, 101, 100));
    }

    [Fact]
    public void AttackerExactlyNineLevelsAbove_StillScales_StaysPositiveBelowBase()
    {
        // gap 9 is the last non-zeroed step: a small positive remainder, never the full base and never 0.
        var scaled = PvpKillExperienceScaling.Scale(110, 109, 100);

        Assert.True(scaled is > 0 and < 110);
    }

    [Fact]
    public void AttackerTenLevelsAbove_ReturnsZero()
    {
        // gap 10 exceeds the inner EXP threshold (9) even though it is still inside the outer 13-level anti-gank
        // gate applied upstream -- CP/drops may still be earned, base EXP is zero.
        Assert.Equal(0, PvpKillExperienceScaling.Scale(110, 110, 100));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    [InlineData(158, 100)]
    public void AttackerCombinedLevelOutsideValidRange_ReturnsZero(int attacker, int victim)
    {
        Assert.Equal(0, PvpKillExperienceScaling.Scale(110, attacker, victim));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(100, 158)]
    public void VictimCombinedLevelOutsideValidRange_ReturnsZero(int attacker, int victim)
    {
        Assert.Equal(0, PvpKillExperienceScaling.Scale(110, attacker, victim));
    }

    [Fact]
    public void ResolveZoneMultiplier_RegularWarServer_ReturnsHundredFifty()
    {
        Assert.Equal(PvpKillExperienceScaling.RegularWarXpMultiplier,
            PvpKillExperienceScaling.ResolveZoneMultiplier(true, 2));
    }

    [Fact]
    public void ResolveZoneMultiplier_NonRegularWarServer_ReturnsConfiguredRatio()
    {
        Assert.Equal(2, PvpKillExperienceScaling.ResolveZoneMultiplier(false, 2));
    }

    [Fact]
    public void ScaledBaseFedThroughComputeGain_ProducesFullPipelineAmount()
    {
        // End-to-end composition the wiring performs: table -> scaling -> ComputeGain multipliers. A regular-war
        // kill (x150) with a warrior scroll (x2), attacker 3 levels below victim (up-scaled 110 -> 143):
        // 143 * 150 * 2 == 42900.
        var scaledBase = PvpKillExperienceScaling.Scale(PvpKillExperienceBaseTable.Lookup(100), 97, 100);
        var multiplier = PvpKillExperienceScaling.ResolveZoneMultiplier(true, 2);

        var gain = PvpKillExperienceCalculator.ComputeGain(scaledBase, 97, 100, true, false, multiplier);

        Assert.Equal(42900, gain);
    }
}
