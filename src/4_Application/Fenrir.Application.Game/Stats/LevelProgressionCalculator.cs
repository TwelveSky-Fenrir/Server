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

    // Flat one-shot skill-point milestone bonuses — LNW33 / production ReleaseEU33 branch.
    // Ref. C++: Server/ts25zone/S07_MyGame03.cpp:266-276. Each is granted once per level-up
    // event, tested against the PRE-GAIN level (never per level crossed, never the destination
    // level), the two are mutually exclusive, and both add to the same skill-point accumulator
    // that receives the per-level ReturnLevelFactor3 gains — with no cap applied here. The
    // #ifndef LNW33 variant (S07_MyGame03.cpp:256-262: +1000 at the max-level milestone and no
    // level-32 bonus) is dead code in ReleaseEU33 and is deliberately NOT reproduced.
    private const int MaxLevelMilestoneSkillBonus = 2000; // pre-gain level 144 (present + 1 == MaxLevel)
    private const int IntermediateMilestoneSkillBonus = 1000; // pre-gain level 32 (present + LvM1 == MaxLevel)
    private const short LvM1 = 113; // LV_M1 (DEFINE.h:451); MaxLevel - LvM1 == 32

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

        // Flat one-shot skill-point milestones: evaluated once against the pre-gain level and
        // mutually exclusive. The milestone levels (144 and 32) are derived from the cited
        // legacy relationships present+1==MaxLevel and present+LvM1==MaxLevel.
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
