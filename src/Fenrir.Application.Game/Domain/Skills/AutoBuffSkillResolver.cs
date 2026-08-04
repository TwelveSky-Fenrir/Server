using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Skills;

public static class AutoBuffSkillResolver
{
    public const int SlotCount = 8;

    public static ImmutableArray<(int SkillId, int Grade)> ResolveRegistration(IReadOnlyList<int> flatSkillGrades,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills, Func<int, bool> isKnownSkill)
    {
        ArgumentNullException.ThrowIfNull(flatSkillGrades);
        ArgumentNullException.ThrowIfNull(learnedSkills);
        ArgumentNullException.ThrowIfNull(isKnownSkill);

        var builder = ImmutableArray.CreateBuilder<(int SkillId, int Grade)>(SlotCount);
        for (var i = 0; i < SlotCount; i++)
        {
            var sourceIndex = i * 2;
            var skillId = sourceIndex < flatSkillGrades.Count ? flatSkillGrades[sourceIndex] : 0;
            var grade = sourceIndex + 1 < flatSkillGrades.Count ? flatSkillGrades[sourceIndex + 1] : 0;

            builder.Add(TryResolveOwnedAutoBuff(skillId, grade, learnedSkills, isKnownSkill, out var selection)
                ? (selection.SkillId, selection.BaseGrade)
                : (0, 0));
        }

        return builder.MoveToImmutable();
    }

    public static bool TryResolveOwnedAutoBuff(int skillId, int requestedBaseGrade,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills, Func<int, bool> isKnownSkill,
        out AutoBuffSelection selection)
    {
        ArgumentNullException.ThrowIfNull(isKnownSkill);
        selection = default;

        if (!isKnownSkill(skillId) || !IsEligibleSkill(skillId) ||
            !TryResolveOwnedSkill(skillId, requestedBaseGrade, learnedSkills, out selection))
            return false;

        return true;
    }

    public static bool TryResolveOwnedSkill(int skillId, int requestedBaseGrade,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills, out AutoBuffSelection selection)
    {
        selection = default;

        if (skillId < 1 || requestedBaseGrade < 1)
            return false;

        var authoritativeGrade = SkillGradeAuthority.GetMaxSkillGradeNum(skillId, learnedSkills);
        if (authoritativeGrade < 1)
            return false;

        selection = new AutoBuffSelection(skillId, Math.Min(requestedBaseGrade, authoritativeGrade));
        return true;
    }

    private static bool IsEligibleSkill(int skillId)
    {
        return SkillEffectCatalog.TryGet(skillId, out var effect) && effect.Kind == SkillEffectKind.SelfBuff;
    }

    public readonly record struct AutoBuffSelection(int SkillId, int BaseGrade);
}
