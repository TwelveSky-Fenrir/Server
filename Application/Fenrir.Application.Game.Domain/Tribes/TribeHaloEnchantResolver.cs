using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Tribes;

public enum TribeHaloEnchantOutcome
{

        Success,

        ProtectionConsumed,

        Downgraded,

        NeutralFail
}

public static class TribeHaloEnchantResolver
{

        public static (int SuccessRate, int DecreaseRate) GetRates(int currentHalo)
    {
        var pCurrentImprove = currentHalo + 1;

        var decrease = pCurrentImprove switch
        {
            >= 1 and <= 9 => 3,
            >= 10 and <= 19 => 6,
            >= 20 and <= 29 => 9,
            >= 30 and <= 39 => 12,
            >= 40 and <= 49 => 15,
            >= 50 and <= 59 => 18,
            >= 60 and <= 69 => 21,
            >= 70 and <= 79 => 24,
            >= 80 and <= 89 => 27,
            _ => 30
        };

        return (15, decrease);
    }

        public static (TribeHaloEnchantOutcome Outcome, int NewHalo, int NewProtectForHalo) Resolve(
        int currentHalo, int currentProtectForHalo, IRandomSource random)
    {
        var (successRate, decreaseRate) = GetRates(currentHalo);
        var successThreshold = successRate + 2;

        if (random.NextInt32(100) < successThreshold)
            return (TribeHaloEnchantOutcome.Success, currentHalo + 1, currentProtectForHalo);

        if (random.NextInt32(100) < decreaseRate && currentHalo > 0)
        {
            if (currentProtectForHalo > 0)
                return (TribeHaloEnchantOutcome.ProtectionConsumed, currentHalo, currentProtectForHalo - 1);

            return (TribeHaloEnchantOutcome.Downgraded, currentHalo - 1, currentProtectForHalo);
        }

        return (TribeHaloEnchantOutcome.NeutralFail, currentHalo, currentProtectForHalo);
    }
}
