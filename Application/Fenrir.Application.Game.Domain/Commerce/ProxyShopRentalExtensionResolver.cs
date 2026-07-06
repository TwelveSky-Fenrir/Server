using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     Pure resolver for the proxy-shop rental-extension consumables handled by CZ_USE_INVENTORY_ITEM_SEND
///     (op23): world.Items 567/592/8422/8423. No I/O -- looking up the character's current
///     game.OfflineShops.ShopDate, persisting the new one, logging the use, and decrementing the item are all
///     the caller's job.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2632-2675 (item-identifier-to-day-count mapping and the
///     date computation/failure check) ; Server/Header/datetime.h:195-218 (date-addition routine) and
///     Server/Header/datetime.h:120-193 (the day-difference helper it calls when the stored date is still in
///     the future) -- the extend-from-existing-expiration-or-from-today rule and the negative-one failure
///     sentinel.
/// </remarks>
public static class ProxyShopRentalExtensionResolver
{
    public enum Outcome
    {
        /// <summary>The item id is not one of the four recognized proxy-shop rental-extension items.</summary>
        NotRecognized,

        /// <summary>The projected new expiration could not be represented as a real calendar date.</summary>
        InvalidDate,

        Success
    }

    /// <summary>
    ///     567/8422 grant one day, 592/8423 grant seven -- two distinct item ids sharing each day count, most
    ///     likely two eras/reskins of the same consumable rather than a behavioral difference.
    /// </summary>
    public static int? ExtensionDaysFor(int itemId)
    {
        return itemId switch
        {
            567 or 8422 => 1,
            592 or 8423 => 7,
            _ => null
        };
    }

    /// <summary>
    ///     Extends from the character's existing <paramref name="currentExpirationDate" /> when it is still
    ///     later than <paramref name="today" /> (compounds onto the remaining time, landing the extension
    ///     days past the existing expiration); otherwise extends from <paramref name="today" /> with no
    ///     compounding. Returns <see cref="Outcome.InvalidDate" /> (carrying <see cref="GameDate.Invalid" />)
    ///     if the projected date cannot be represented as a real calendar date -- only reachable in practice
    ///     via an already-extreme stored expiration, since the extension lengths themselves are fixed
    ///     one/seven-day constants.
    /// </summary>
    public static ExtensionResult Resolve(int itemId, int today, int currentExpirationDate)
    {
        if (ExtensionDaysFor(itemId) is not { } extensionDays)
            return new ExtensionResult(Outcome.NotRecognized, GameDate.Invalid);

        var baseDate = currentExpirationDate > today ? currentExpirationDate : today;

        return GameDate.TryAddDays(baseDate, extensionDays, out var newExpiration)
            ? new ExtensionResult(Outcome.Success, newExpiration)
            : new ExtensionResult(Outcome.InvalidDate, GameDate.Invalid);
    }

    public readonly record struct ExtensionResult(Outcome Outcome, int NewExpirationDate);
}
