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
        // 50,000,000 - 20% = 40,000,000.
        Assert.Equal(40_000_000, PremiumPricing.ApplyPremiumDiscount(50_000_000, true));
    }

    [Fact]
    public void Premium_TwoBillionBasePrice_DoesNotOverflow()
    {
        // The intermediate (base * 20) exceeds int.MaxValue -- must be computed in 64-bit. 2,000,000,000 - 20%
        // = 1,600,000,000.
        Assert.Equal(1_600_000_000, PremiumPricing.ApplyPremiumDiscount(2_000_000_000, true));
    }

    [Fact]
    public void Premium_FloorsTheDiscount()
    {
        // 99 * 20 / 100 = 19 (floored); 99 - 19 = 80.
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
