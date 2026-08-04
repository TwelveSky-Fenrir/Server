using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarRewardValueProvider(WorldDataCache worldData) : IRegularWarRewardValueProvider
{
    private const int FirstTierExperienceMultiplier = 10;

    private const int SecondTierExperienceMultiplier = 5;

    private const long MaxNumberSizeSentinel = 2_000_000_000L;

    public long GetMoneyReward(short evolutionTier, short level)
    {
        if (evolutionTier > 0)
            return evolutionTier switch
            {
                <= 4 => 10_000_000,
                <= 8 => 15_000_000,
                <= 12 => 20_000_000,
                _ => 0
            };

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
        var (rangeMin, rangeMax) = LevelExperienceRange(level);

        var raw = level switch
        {
            >= ExperienceFormulas.MaxLimitLevel => MaxNumberSizeSentinel - rangeMin,
            >= ExperienceFormulas.RebirthDivisorLevelThreshold =>
                (rangeMax - rangeMin) * SecondTierExperienceMultiplier,
            _ => (rangeMax - rangeMin) * FirstTierExperienceMultiplier
        };

        return raw <= 0 ? 0 : (int)Math.Ceiling(raw / 100.0);
    }

    private (long RangeMin, long RangeMax) LevelExperienceRange(short level)
    {
        if (level < 1 || level > ExperienceFormulas.MaxLimitLevel)
            return (0L, 0L);

        return worldData.LevelsByLevel.TryGetValue(level, out var row)
            ? (row.ExpRangeMin, row.ExpRangeMax)
            : (0L, 0L);
    }
}
