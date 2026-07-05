using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Pure resolver for CZ_CONTINUE_SKILL_STAT_SEND (op94). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Mirrors <c>MyUtil::GetMaxSkillGradeNum</c> exactly: it scans the character's own learned-skill slots for
///     the requested skill id and returns its current learned grade, or -1 if the skill isn't learned at all --
///     NOT the skill's catalog-defined max grade. Requesting an auto-buff for an unlearned skill therefore always
///     clamps to grade -1, reproduced here verbatim rather than "fixed" (D8).
/// </remarks>
public static class AutoBuffSkillResolver
{
    /// <summary>MAX_AUTO_BUFF_SKILL_NUM.</summary>
    public const int SlotCount = 8;

    public static ImmutableArray<(int SkillId, int Grade)> ResolveRegistration(IReadOnlyList<int> flatSkillGrades,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        var builder = ImmutableArray.CreateBuilder<(int SkillId, int Grade)>(SlotCount);
        for (var i = 0; i < SlotCount; i++)
        {
            var skillId = flatSkillGrades[i * 2];
            var grade = flatSkillGrades[i * 2 + 1];
            var maxGrade = GetMaxSkillGradeNum(skillId, learnedSkills);
            if (grade > maxGrade)
                grade = maxGrade;
            builder.Add((skillId, grade));
        }

        return builder.MoveToImmutable();
    }

    private static int GetMaxSkillGradeNum(int skillId, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == skillId)
                return learned.Grade;

        return -1;
    }
}
