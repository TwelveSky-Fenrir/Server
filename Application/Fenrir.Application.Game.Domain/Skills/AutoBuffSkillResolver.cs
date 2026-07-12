using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Skills;

public static class AutoBuffSkillResolver
{
    public const int SlotCount = 8;

    public static ImmutableArray<(int SkillId, int Grade)> ResolveRegistration(IReadOnlyList<int> flatSkillGrades,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        var builder = ImmutableArray.CreateBuilder<(int SkillId, int Grade)>(SlotCount);
        for (var i = 0; i < SlotCount; i++)
        {
            var skillId = flatSkillGrades[i * 2];
            var grade = flatSkillGrades[i * 2 + 1];
            var maxGrade = SkillGradeAuthority.GetMaxSkillGradeNum(skillId, learnedSkills);
            if (grade > maxGrade)
                grade = maxGrade;
            builder.Add((skillId, grade));
        }

        return builder.MoveToImmutable();
    }
}
