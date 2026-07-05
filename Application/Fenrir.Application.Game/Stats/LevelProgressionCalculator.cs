using System.Collections.Frozen;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Stats;

/// <summary>
///     Outcome of one <see cref="LevelProgressionCalculator.ResolveLevelUp" /> call. <see cref="NewLevel" /> is
///     always the resolved level (even when <see cref="LeveledUp" /> is false, in which case it equals the
///     level the pre-gain experience already resolves to).
/// </summary>
public readonly record struct LevelUpResult(
    short NewLevel,
    int StatPointsGranted,
    int SkillPointsGranted,
    bool LeveledUp);

/// <summary>
///     Pure C# port of the legacy <c>MyUtil::ProcessForExperience</c>'s level-up loop (<c>S07_MyGame03.cpp:228-296</c>)
///     plus <c>LEVELSYSTEM::ReturnLevel</c>/<c>ReturnLevelFactor3</c> (<c>GameSystem_01_Level.cpp</c>). No I/O:
///     every input is a plain value the caller already has in memory.
/// </summary>
/// <remarks>
///     Not modeled (both are the legacy's own "already at the level cap" fork,
///     <c>avt-&gt;aLevel1 == MAX_LIMIT_LEVEL_NUM</c>,
///     a wholly different code path): the level-145 XP-to-stat-point trickle (1% of the remaining range = +1
///     aStatPoint) and the post-cap <c>ProcessForExperience2</c>/<c>aLevel2</c> "high level" rebirth ladder.
///     <see cref="ResolveLevelUp" /> simply reports no level-up once <see cref="MaxLevel" /> is reached, matching
///     <c>ReturnLevel</c>'s own behavior of never resolving past it.
/// </remarks>
public static class LevelProgressionCalculator
{
    /// <summary><c>MAX_LIMIT_LEVEL_NUM</c> (DEFINE.h) -- <c>ReturnLevel</c> never resolves above this.</summary>
    public const short MaxLevel = 145;

    /// <summary>
    ///     Resolves how much of <paramref name="experienceGain" /> lands as a level-up, and the stat/skill
    ///     points that level-up grants. Levels are always re-derived from experience via <see cref="ReturnLevel" />
    ///     (matching the legacy re-deriving both endpoints from <c>avt-&gt;aExp1</c> rather than trusting the
    ///     stored <c>aLevel1</c>) -- a multi-level gain in one kill is applied as a single loop over every level
    ///     crossed, exactly like the source's own <c>for (index01 = present+1; index01 &lt;= next; index01++)</c>.
    /// </summary>
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
            // The tier check is against the level BEFORE this step (index01-1 in the source), not the level
            // being reached -- e.g. reaching level 100 (prior level 99) is the first step in the 15-point tier.
            var priorLevel = level - 1;
            statPoints += priorLevel switch
            {
                < 99 => 5,
                < 112 => 15,
                _ => 30
            };
            skillPoints += levels.TryGetValue((short)level, out var row) ? row.RangeInfo3 : 0;
        }

        return new LevelUpResult(nextLevel, statPoints, skillPoints, true);
    }

    /// <summary>
    ///     <c>LEVELSYSTEM::ReturnLevel</c>: below level 1's own XP floor resolves to 1; a linear scan of levels
    ///     1..144's own <c>[ExpRangeMin,ExpRangeMax]</c> band; anything past level 144's own range (not just
    ///     past level 145's) resolves to <see cref="MaxLevel" /> regardless of level 145's own band.
    /// </summary>
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
