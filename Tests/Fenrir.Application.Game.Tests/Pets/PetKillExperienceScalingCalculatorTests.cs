using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetKillExperienceScalingCalculatorTests
{
    [Fact]
    public void ComputeScaledAmount_BaseAmountNotPositive_ReturnsZero()
    {
        Assert.Equal(0, PetKillExperienceScalingCalculator.ComputeScaledAmount(0, 0f, false, false));
        Assert.Equal(0, PetKillExperienceScalingCalculator.ComputeScaledAmount(-5, 0f, false, false));
    }

    [Fact]
    public void ComputeScaledAmount_GlobalRatioOnly_MultipliesByTwenty()
    {
        Assert.Equal(20, PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0f, false, false));
        Assert.Equal(200, PetKillExperienceScalingCalculator.ComputeScaledAmount(10, 0f, false, false));
    }

    [Fact]
    public void ComputeScaledAmount_PersonalAddOn_AppliesTenPercentOfTheAlreadyScaledAmount_NotTheRawBase()
    {
        Assert.Equal(22, PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0.1f, false, false));
    }

    [Fact]
    public void ComputeScaledAmount_PersonalAddOnInactive_NoAdditionApplied()
    {
        Assert.Equal(20, PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0f, false, false));
    }

    [Fact]
    public void ComputeScaledAmount_DoubleExpTimerActive_DoublesIndependently()
    {
        Assert.Equal(40, PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0f, true, false));
    }

    [Fact]
    public void ComputeScaledAmount_PremiumActive_DoublesIndependently()
    {
        Assert.Equal(40, PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0f, false, true));
    }

    [Fact]
    public void ComputeScaledAmount_RichestLegacyCase_IsEightyEightTimesBase()
    {
        var result = PetKillExperienceScalingCalculator.ComputeScaledAmount(1, 0.1f, true, true);

        Assert.Equal(88, result);
    }

    [Fact]
    public void ComputeScaledAmount_RichestLegacyCase_ScalesLinearlyWithBase()
    {
        Assert.Equal(880, PetKillExperienceScalingCalculator.ComputeScaledAmount(10, 0.1f, true, true));
    }

    [Fact]
    public void GlobalRatio_IsExactlyTwenty()
    {
        Assert.Equal(20, PetKillExperienceScalingCalculator.GlobalRatio);
    }
}
