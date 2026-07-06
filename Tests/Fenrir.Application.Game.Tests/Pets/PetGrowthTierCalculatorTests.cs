using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers <see cref="PetGrowthTierCalculator" /> against <c>PETSYSTEM::ReturnGrowStep</c>
///     (GameSystem_07_Pet.cpp:335-435), including the deliberate discrepancy with
///     <see cref="PetExperienceCreditCalculator" />'s own, different category table.
/// </summary>
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
    [InlineData(1_000, 0)] // 0.0025% -> tier 0
    [InlineData(10_000_000, 1)] // 25% of 40,000,000 -> tier 1
    [InlineData(20_000_000, 2)] // 50% -> tier 2
    [InlineData(30_000_000, 3)] // 75% -> tier 3
    [InlineData(40_000_000, 4)] // at cap -> tier 4 ("100%+")
    [InlineData(50_000_000, 4)] // above cap -> still tier 4
    public void ComputeTier_Category0Item_BucketsByPercentOfCap(int growth, int expectedTier)
    {
        // item 541 -> tier category 0, cap 40,000,000 (same array slot the credit table also calls category 0).
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
        // item 1002: PetExperienceCreditCalculator places it in category 4 (cap 80,000,000), but
        // PetGrowthTierCalculator's own, separate table places it in category 0 (cap 40,000,000) instead --
        // a genuine legacy inconsistency (see both classes' remarks), reproduced faithfully here.
        Assert.True(PetExperienceCreditCalculator.TryResolveCategory(1002, out var creditCategory));
        Assert.Equal(4, creditCategory);

        const int growth = 50_000_000; // below the real (credit-table) cap of 80,000,000...
        Assert.True(growth < PetGrowthCaps.Values[creditCategory]);

        // ...but the tier table evaluates this same growth value against half that cap and already reports
        // the pet as fully grown (tier 4), a full 30,000,000 before the credit table would agree.
        Assert.Equal(4, PetGrowthTierCalculator.ComputeTier(1002, growth));
    }
}
