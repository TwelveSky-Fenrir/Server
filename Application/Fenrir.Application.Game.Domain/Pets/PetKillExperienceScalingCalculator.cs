namespace Fenrir.Application.Game.Domain.Pets;

public static class PetKillExperienceScalingCalculator
{
    public const int GlobalRatio = 20;

    public static int ComputeScaledAmount(
        int baseAmount,
        float personalAddOnRatio,
        bool doubleExpTimerActive,
        bool premiumActive)
    {
        if (baseAmount <= 0)
            return 0;

        var scaled = baseAmount;

        if (GlobalRatio > 1)
            scaled *= GlobalRatio;

        if (personalAddOnRatio > 0f)
            scaled += (int)(scaled * personalAddOnRatio);

        if (doubleExpTimerActive)
            scaled *= 2;

        if (premiumActive)
            scaled *= 2;

        return scaled;
    }
}
