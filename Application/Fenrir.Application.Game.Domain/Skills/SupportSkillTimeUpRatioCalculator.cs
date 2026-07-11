namespace Fenrir.Application.Game.Domain.Skills;

public static class SupportSkillTimeUpRatioCalculator
{
    public static int Compute(bool buffDurationExtensionActive, bool premiumActive)
    {
        var ratio = 1;

        if (buffDurationExtensionActive)
            ratio *= 2;

        if (premiumActive)
            ratio *= 2;

        return ratio;
    }
}
