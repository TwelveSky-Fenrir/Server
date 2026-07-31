using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarRewardValueProvider : IRegularWarRewardValueProvider
{
    private const int FirstTierExperienceMultiplier = 10;

    private const int SecondTierExperienceMultiplier = 5;

    private const int MaxNumberSizeSentinel = 2_000_000_000;

    public long GetMoneyReward(short evolutionTier, short level)
    {
        // un tier martial > 0 court-circuite la table de niveau - Server/ts25zone/S07_MyGame01.cpp:6728
        if (evolutionTier > 0)
            return evolutionTier switch
            {
                <= 4 => 10_000_000,
                <= 8 => 15_000_000,
                <= 12 => 20_000_000,
                _ => 0
            };

        // variante LNW33 uniquement - Server/ts25zone/S07_MyGame01.cpp:7336-7882
        return level switch
        {
            < 10 => 0,
            <= 29 => 100_000,
            <= 44 => 300_000,
            <= 69 => 500_000,
            <= 89 => 700_000,
            <= 112 => 1_000_000,
            <= 123 => 2_000_000,
            <= 134 => 3_000_000,
            <= 144 => 4_000_000,
            145 => 5_000_000,
            _ => 0
        };
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
