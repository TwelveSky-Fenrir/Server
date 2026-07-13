using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarRewardValueProvider : IRegularWarRewardValueProvider
{
    private const int FirstTierExperienceMultiplier = 10;

    private const int SecondTierExperienceMultiplier = 5;

    private const int MaxNumberSizeSentinel = 2_000_000_000;

    public long GetMoneyReward(short evolutionTier, short level)
    {
        return 0;
    }

    public int GetExperienceReward(short level)
    {
        var raw = level switch
        {
            >= ExperienceFormulas.MaxLimitLevel => MaxNumberSizeSentinel,
            >= ExperienceFormulas.RebirthDivisorLevelThreshold =>
                LevelCurveDifference(level) * SecondTierExperienceMultiplier,
            _ => LevelCurveDifference(level) * FirstTierExperienceMultiplier
        };

        return (int)Math.Ceiling(raw / 100.0);
    }

    private static int LevelCurveDifference(short level)
    {
        return ExperienceFormulas.ReturnFixedLevel(level + 1) - ExperienceFormulas.ReturnFixedLevel(level);
    }
}
