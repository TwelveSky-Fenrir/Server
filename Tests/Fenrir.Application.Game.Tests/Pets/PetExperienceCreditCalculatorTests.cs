using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers <see cref="PetExperienceCreditCalculator" /> against the monster-kill call shape of
///     <c>PETSYSTEM::ReturnExperience</c> (GameSystem_07_Pet.cpp:1804-1918, <c>pGrowUpValue</c> always 0.0f
///     from that call site).
/// </summary>
public class PetExperienceCreditCalculatorTests
{
    [Theory]
    [InlineData(541, 0)]
    [InlineData(1004, 4)]
    [InlineData(1310, 7)]
    [InlineData(86820, 2)]
    public void TryResolveCategory_KnownItemIds_ResolveToTheDocumentedCategory(int itemId, int expectedCategory)
    {
        var resolved = PetExperienceCreditCalculator.TryResolveCategory(itemId, out var category);

        Assert.True(resolved);
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void TryResolveCategory_UnrecognizedItemId_ReturnsFalse()
    {
        var resolved = PetExperienceCreditCalculator.TryResolveCategory(999_999, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void ComputeCreditedAmount_UnrecognizedItemId_CreditsNothing()
    {
        Assert.Equal(0, PetExperienceCreditCalculator.ComputeCreditedAmount(999_999, 0, 800));
    }

    [Fact]
    public void ComputeCreditedAmount_RequestedAmountNotPositive_CreditsNothing()
    {
        Assert.Equal(0, PetExperienceCreditCalculator.ComputeCreditedAmount(541, 0, 0));
        Assert.Equal(0, PetExperienceCreditCalculator.ComputeCreditedAmount(541, 0, -10));
    }

    [Fact]
    public void ComputeCreditedAmount_GrowthAlreadyAtCap_CreditsNothing()
    {
        // item 541 -> category 0, cap 40,000,000.
        Assert.Equal(0, PetExperienceCreditCalculator.ComputeCreditedAmount(541, 40_000_000, 800));
    }

    [Fact]
    public void ComputeCreditedAmount_GrowthAboveCap_CreditsNothing()
    {
        Assert.Equal(0, PetExperienceCreditCalculator.ComputeCreditedAmount(541, 50_000_000, 800));
    }

    [Fact]
    public void ComputeCreditedAmount_WellBelowCap_CreditsTheFullRequestedAmount()
    {
        Assert.Equal(800, PetExperienceCreditCalculator.ComputeCreditedAmount(541, 1000, 800));
    }

    [Fact]
    public void ComputeCreditedAmount_WouldCrossTheCap_ClampsExactlyToTheCap()
    {
        // item 546 -> category 3, cap 320,000,000; 5 short of the cap but requesting 800.
        const int cap = 320_000_000;
        var credited = PetExperienceCreditCalculator.ComputeCreditedAmount(546, cap - 5, 800);

        Assert.Equal(5, credited);
    }

    [Fact]
    public void ComputeCreditedAmount_HigherCategoryUsesItsOwnLargerCap()
    {
        // item 1016 -> category 7, cap 640,000,000 -- well above a lower category's cap, still creditable.
        const int growth = 320_000_001;
        Assert.Equal(800, PetExperienceCreditCalculator.ComputeCreditedAmount(1016, growth, 800));
    }
}
