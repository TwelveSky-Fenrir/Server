using Fenrir.Application.Game.Domain.Economy;

namespace Fenrir.Application.Game.Tests.Economy;

public class PremiumPricingTests
{
    [Fact]
    public void NonPremium_ReturnsBasePriceUnchanged()
    {
        Assert.Equal(50_000_000, PremiumPricing.ApplyPremiumDiscount(50_000_000, false));
    }

    [Fact]
    public void Premium_Subtracts20Percent()
    {
        Assert.Equal(40_000_000, PremiumPricing.ApplyPremiumDiscount(50_000_000, true));
    }

    [Fact]
    public void Premium_TwoBillionBasePrice_DoesNotOverflow()
    {
        Assert.Equal(1_600_000_000, PremiumPricing.ApplyPremiumDiscount(2_000_000_000, true));
    }

    [Fact]
    public void Premium_FloorsTheDiscount()
    {
        Assert.Equal(80, PremiumPricing.ApplyPremiumDiscount(99, true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Premium_NonPositiveBasePrice_ReturnedUnchanged(int basePrice)
    {
        Assert.Equal(basePrice, PremiumPricing.ApplyPremiumDiscount(basePrice, true));
    }
}
