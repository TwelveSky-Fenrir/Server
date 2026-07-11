namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountKillExperienceCalculator
{

        public const int PlaceholderBaseExperiencePerKill = 10;

        public static int ComputeGain(
        bool isMounted, int mountActivity, int mountExperience,
        bool hasDoubleExp, bool hasSessionExpUp,
        int baseAmount = PlaceholderBaseExperiencePerKill)
    {
        if (!isMounted || mountActivity <= 0 || mountExperience >= MountActivityExpCodec.MaxExp)
            return 0;

        var amount = baseAmount;
        if (hasDoubleExp)
            amount *= 2;
        if (hasSessionExpUp)
            amount *= 2;
        return amount;
    }
}
