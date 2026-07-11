using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers <see cref="PetKillExperienceScalingCalculator" /> against <c>MONSTER_OBJECT::ProcessForExp</c>
///     (<c>S07_MyGame05.cpp:3855-3863</c>) -- the B8-pet-growth-depth contract's Part C: the global x20
///     ratio, the personal 10% add-on, and the two independent doublings, applied in that exact order.
/// </summary>
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
        // base=1 -> x20 = 20 -> +10% of 20 (=2) -> 22. NOT 1 + 10% of 1.
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
        // base=1 -> x20=20 -> +10%=22 -> x2(timer)=44 -> x2(premium)=88. The contract's own documented
        // "single richest legacy case" aggregate multiplier.
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
