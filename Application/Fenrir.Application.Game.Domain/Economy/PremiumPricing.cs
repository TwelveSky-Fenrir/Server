namespace Fenrir.Application.Game.Domain.Economy;

/// <summary>
///     Shared premium-status price adjustment for the item-modification economy (enchant / forge / stellar).
///     Port of the <c>#ifndef NO_DISCOUNT_FOR_PREMIUM</c> block that every priced legacy helper runs after its
///     own table lookup (Server/Header/function.h:451-461): a flat 20% reduction of the already-computed price
///     for a character whose Premium status is currently active. <c>NO_DISCOUNT_FOR_PREMIUM</c> is not defined
///     in the shipped ReleaseEU33 build, so the discount is live.
/// </summary>
/// <remarks>
///     The "is Premium currently active" determination is the caller's job -- it is
///     <c>PlayerRuntimeState.PremiumExpireUtc &gt;= nowUnixSeconds</c> (see
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.PremiumExpireUtc" />), resolved at the
///     service layer and passed in as a bool so this stays a pure, allocation-free value computation with no
///     clock/state dependency. Not every priced path applies the discount: the costume-enchant price table
///     (function.h:512-523) is explicitly outside the <c>NO_DISCOUNT_FOR_PREMIUM</c> guard, so
///     <see cref="Fenrir.Application.Game.Domain.Enchant.CostumeImproveResolver" /> does NOT call this helper --
///     only the paths whose legacy price lookup sits inside the guard (stellar-core merge, the ordinary
///     improve/high/low tables) do.
/// </remarks>
public static class PremiumPricing
{
    /// <summary>Premium reduces the price by this many percent of the already-computed base price.</summary>
    public const int PremiumDiscountPercent = 20;

    /// <summary>
    ///     Returns <paramref name="basePrice" /> unchanged for a non-premium character, or with a flat
    ///     <see cref="PremiumDiscountPercent" />% subtracted (integer-floored, matching the legacy's
    ///     <c>tMoney -= tMoney * 20 / 100</c>) for an active-premium one. Computed in 64-bit to avoid the
    ///     32-bit overflow a 2,000,000,000-scale base price would hit at the intermediate <c>* 20</c> step.
    /// </summary>
    public static int ApplyPremiumDiscount(int basePrice, bool isPremium)
    {
        if (!isPremium || basePrice <= 0)
            return basePrice;

        var discount = (int)((long)basePrice * PremiumDiscountPercent / 100);
        return basePrice - discount;
    }
}
