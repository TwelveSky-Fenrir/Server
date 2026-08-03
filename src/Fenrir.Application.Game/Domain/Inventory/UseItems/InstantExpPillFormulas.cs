using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public static class InstantExpPillFormulas
{
    public static bool IsInstantExpPill(int itemId)
    {
        return itemId is 649 or 650 or 1489 or 1490;
    }

    public static bool IsAtAbsoluteExpCeiling(short level, long experience)
    {
        return level == LevelProgressionCalculator.MaxLevel
               && experience >= HighLevelExperienceResolver.MaxMainExperience;
    }

    public static int ComputeLevelBandGain(
        short level,
        FrozenDictionary<short, LevelRowDto> levelsByLevel,
        int multiplier)
    {
        if (!levelsByLevel.TryGetValue(level, out var row))
            return 0;

        var factor1 = (long)row.ExpRangeMin;
        var factor2 = level == LevelProgressionCalculator.MaxLevel
            ? HighLevelExperienceResolver.MaxMainExperience
            : row.ExpRangeMax;

        var band = factor2 - factor1;
        if (band <= 0)
            return 0;

        var baseUnit = (int)((band + 99L) / 100L);
        return baseUnit * multiplier;
    }

    public static int ComputeRebirthTierGain(short level2)
    {
        if (level2 < 1 || level2 > RebirthProgression.MaxHighLevel)
            return 0;

        var threshold = (long)RebirthProgression.HighLevelExpTable[level2 - 1];
        return (int)((threshold + 99L) / 100L);
    }
}
