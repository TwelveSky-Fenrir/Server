using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class HermitsVaultRenewalPolicy
{
    public static bool TryComputeRenewedExpiry(int currentExpiry, int today, int dayCount, out int newExpiry)
    {
        if (dayCount < 0)
        {
            newExpiry = GameDate.Invalid;
            return false;
        }

        var baseline = currentExpiry >= today ? currentExpiry : today;
        return GameDate.TryAddDays(baseline, dayCount, out newExpiry);
    }
}
