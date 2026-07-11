using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.Commerce;

public static class ProxyShopRentalExtensionResolver
{
    public enum Outcome
    {

                NotRecognized,

                InvalidDate,

        Success
    }

        public static int? ExtensionDaysFor(int itemId)
    {
        return itemId switch
        {
            567 or 8422 => 1,
            592 or 8423 => 7,
            _ => null
        };
    }

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
