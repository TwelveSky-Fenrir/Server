using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountKillExperienceCalculatorTests
{
    private const int Base = MountKillExperienceCalculator.PlaceholderBaseExperiencePerKill;

    [Fact]
    public void ComputeGain_MountedAndFedAndBelowCap_ReturnsBaseAmount()
    {
        Assert.Equal(Base, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0,
            false, false));
    }

    [Fact]
    public void ComputeGain_DoubleExpFlag_DoublesTheAmount()
    {
        Assert.Equal(Base * 2, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, true, false));
    }

    [Fact]
    public void ComputeGain_SessionExpUpFlag_DoublesTheAmount()
    {
        Assert.Equal(Base * 2, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, false, true));
    }

    [Fact]
    public void ComputeGain_BothMultipliers_Quadruple()
    {
        Assert.Equal(Base * 4, MountKillExperienceCalculator.ComputeGain(
            true, 50, 0, true, true));
    }

    [Fact]
    public void ComputeGain_NotMounted_IsZero()
    {
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            false, 50, 0, true, true));
    }

    [Fact]
    public void ComputeGain_UnfedMount_IsZero()
    {
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            true, 0, 0, false, false));
    }

    [Fact]
    public void ComputeGain_AtOrAboveExperienceCap_IsZero()
    {
        Assert.Equal(0, MountKillExperienceCalculator.ComputeGain(
            true, 50, MountActivityExpCodec.MaxExp, false, false));
        Assert.Equal(Base, MountKillExperienceCalculator.ComputeGain(
            true, 50, MountActivityExpCodec.MaxExp - 1, false,
            false));
    }

    [Fact]
    public void ComputeGain_HonorsAConfiguredBaseAmount()
    {
        Assert.Equal(25, MountKillExperienceCalculator.ComputeGain(
            true, 1, 0, false, false, 25));
    }
}
