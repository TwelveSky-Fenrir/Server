using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

public readonly record struct LevelUpResult(
    short NewLevel,
    int StatPointsGranted,
    int SkillPointsGranted,
    bool LeveledUp);

public static class LevelProgressionCalculator
{
    public const short MaxLevel = 145;

    private const int MaxLevelMilestoneSkillBonus = 2000;
    private const int IntermediateMilestoneSkillBonus = 1000;
    private const short LvM1 = 113;

    public static LevelUpResult ResolveLevelUp(long currentExperience, long experienceGain,
        FrozenDictionary<short, LevelRowDto> levels)
    {
        var presentLevel = ReturnLevel(currentExperience, levels);
        if (experienceGain <= 0)
            return new LevelUpResult(presentLevel, 0, 0, false);

        var nextLevel = ReturnLevel(currentExperience + experienceGain, levels);
        if (nextLevel <= presentLevel)
            return new LevelUpResult(presentLevel, 0, 0, false);

        var statPoints = 0;
        var skillPoints = 0;
        for (var level = presentLevel + 1; level <= nextLevel; level++)
        {
            var priorLevel = level - 1;
            statPoints += priorLevel switch
            {
                < 99 => 5,
                < 112 => 15,
                _ => 30
            };
            skillPoints += levels.TryGetValue((short)level, out var row) ? row.RangeInfo3 : 0;
        }

        skillPoints += presentLevel switch
        {
            MaxLevel - 1 => MaxLevelMilestoneSkillBonus,
            MaxLevel - LvM1 => IntermediateMilestoneSkillBonus,
            _ => 0
        };

        return new LevelUpResult(nextLevel, statPoints, skillPoints, true);
    }

    private static short ReturnLevel(long experience, FrozenDictionary<short, LevelRowDto> levels)
    {
        if (!levels.TryGetValue(1, out var firstRow) || experience < firstRow.ExpRangeMin)
            return 1;

        for (short level = 1; level < MaxLevel; level++)
            if (levels.TryGetValue(level, out var row) && experience >= row.ExpRangeMin &&
                experience <= row.ExpRangeMax)
                return level;

        return MaxLevel;
    }
}
