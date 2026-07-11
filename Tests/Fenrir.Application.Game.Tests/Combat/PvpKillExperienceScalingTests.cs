using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillExperienceScalingTests
{
    [Fact]
    public void EqualCombinedLevels_ReturnsBaseUnchanged()
    {
        Assert.Equal(110, PvpKillExperienceScaling.Scale(110, 100, 100));
    }

    [Fact]
    public void AttackerBelowVictim_UpScalesTenPercentPerLevel()
    {
        Assert.Equal(143, PvpKillExperienceScaling.Scale(110, 97, 100));
    }

    [Fact]
    public void AttackerOneLevelAboveVictim_DownScalesTenPercent()
    {
        Assert.Equal(99, PvpKillExperienceScaling.Scale(110, 101, 100));
    }

    [Fact]
    public void AttackerExactlyNineLevelsAbove_StillScales_StaysPositiveBelowBase()
    {
        var scaled = PvpKillExperienceScaling.Scale(110, 109, 100);

        Assert.True(scaled is > 0 and < 110);
    }

    [Fact]
    public void AttackerTenLevelsAbove_ReturnsZero()
    {
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
        var scaledBase = PvpKillExperienceScaling.Scale(PvpKillExperienceBaseTable.Lookup(100), 97, 100);
        var multiplier = PvpKillExperienceScaling.ResolveZoneMultiplier(true, 2);

        var gain = PvpKillExperienceCalculator.ComputeGain(scaledBase, 97, 100, true, false, multiplier);

        Assert.Equal(42900, gain);
    }
}
