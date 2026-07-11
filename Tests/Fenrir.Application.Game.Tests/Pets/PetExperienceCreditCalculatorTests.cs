using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

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
        const int cap = 320_000_000;
        var credited = PetExperienceCreditCalculator.ComputeCreditedAmount(546, cap - 5, 800);

        Assert.Equal(5, credited);
    }

    [Fact]
    public void ComputeCreditedAmount_HigherCategoryUsesItsOwnLargerCap()
    {
        const int growth = 320_000_001;
        Assert.Equal(800, PetExperienceCreditCalculator.ComputeCreditedAmount(1016, growth, 800));
    }
}
