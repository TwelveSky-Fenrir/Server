using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountKillExperienceCalculatorTests
{
    private const int Base = MountKillExperienceCalculator.PlaceholderBaseExperiencePerKill;

    [Fact]
    public void ComputeGain_MountedAndFedAndBelowCap_ReturnsBaseAmount()
    {
        Assert.Equal(Base, MountKillExperienceCalculator.ComputeGain(
            isMounted: true, mountActivity: 50, mountExperience: 0,
            hasDoubleExp: false, hasSessionExpUp: false));
    }

    [Fact]
    public void ComputeGain_DoubleExpFlag_DoublesTheAmount()
    {
        Assert.Equal(Base * 2, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, hasDoubleExp: true, hasSessionExpUp: false));
    }

    [Fact]
    public void ComputeGain_SessionExpUpFlag_DoublesTheAmount()
    {
        Assert.Equal(Base * 2, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, hasDoubleExp: false, hasSessionExpUp: true));
    }

    [Fact]
    public void ComputeGain_BothMultipliers_Quadruple()
    {
        Assert.Equal(Base * 4, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, hasDoubleExp: true, hasSessionExpUp: true));
    }

    [Fact]
    public void ComputeGain_NotMounted_IsZero()
    {
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            isMounted: false, mountActivity: 50, mountExperience: 0, hasDoubleExp: true, hasSessionExpUp: true));
    }

    [Fact]
    public void ComputeGain_UnfedMount_IsZero()
    {
        // activity 0 -> the "unfed mount never gains kill experience" gate.
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            true, mountActivity: 0, mountExperience: 0, hasDoubleExp: false, hasSessionExpUp: false));
    }

    [Fact]
    public void ComputeGain_AtOrAboveExperienceCap_IsZero()
    {
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            true, 50, mountExperience: MountActivityExpCodec.MaxExp, hasDoubleExp: false, hasSessionExpUp: false));
        Assert.Equal(Base, MountKillExperienceCalculator.ComputeGain(
            true, 50, mountExperience: MountActivityExpCodec.MaxExp - 1, hasDoubleExp: false,
            hasSessionExpUp: false));
    }

    [Fact]
    public void ComputeGain_HonorsAConfiguredBaseAmount()
    {
        Assert.Equal(25, MountKillExperienceCalculator.ComputeGain(
            true, 1, 0, hasDoubleExp: false, hasSessionExpUp: false, baseAmount: 25));
    }
}
