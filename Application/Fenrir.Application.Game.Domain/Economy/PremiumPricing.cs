namespace Fenrir.Application.Game.Domain.Economy;

public static class PremiumPricing
{

        public const int PremiumDiscountPercent = 20;

        public static int ApplyPremiumDiscount(int basePrice, bool isPremium)
    {
        if (!isPremium || basePrice <= 0)
            return basePrice;

        var discount = (int)((long)basePrice * PremiumDiscountPercent / 100);
        return basePrice - discount;
    }
}
