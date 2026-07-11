using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetGrowthTierCalculatorTests
{
    [Fact]
    public void ComputeTier_GrowthBelowOne_ReturnsZero()
    {
        Assert.Equal(0, PetGrowthTierCalculator.ComputeTier(541, 0));
    }

    [Fact]
    public void ComputeTier_UnrecognizedItemId_ReturnsZero()
    {
        Assert.Equal(0, PetGrowthTierCalculator.ComputeTier(999_999, 20_000_000));
    }

    [Theory]
    [InlineData(1_000, 0)]
    [InlineData(10_000_000, 1)]
    [InlineData(20_000_000, 2)]
    [InlineData(30_000_000, 3)]
    [InlineData(40_000_000, 4)]
    [InlineData(50_000_000, 4)]
    public void ComputeTier_Category0Item_BucketsByPercentOfCap(int growth, int expectedTier)
    {
        Assert.Equal(expectedTier, PetGrowthTierCalculator.ComputeTier(541, growth));
    }

    [Fact]
    public void HasTierIncreased_CrossingA25PercentBoundary_ReturnsTrue()
    {
        Assert.True(PetGrowthTierCalculator.HasTierIncreased(541, 9_000_000, 11_000_000));
    }

    [Fact]
    public void HasTierIncreased_StayingWithinTheSameBucket_ReturnsFalse()
    {
        Assert.False(PetGrowthTierCalculator.HasTierIncreased(541, 1_000, 2_000));
    }

    [Fact]
    public void ComputeTier_ItemInCreditTableHigherCategory_UsesTierTableHalvedCap_LegacyDiscrepancy()
    {
        Assert.True(PetExperienceCreditCalculator.TryResolveCategory(1002, out var creditCategory));
        Assert.Equal(4, creditCategory);

        const int growth = 50_000_000;
        Assert.True(growth < PetGrowthCaps.Values[creditCategory]);

        Assert.Equal(4, PetGrowthTierCalculator.ComputeTier(1002, growth));
    }
}
