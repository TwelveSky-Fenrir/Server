using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Port of SKILLSYSTEM::ReturnSkillValue: linear interpolation between a skill's two SkillGrades rows
///     (GradeIndex 0 = min, 1 = max) by the caster's invested grade points, out of MaxUpgradePoint.
/// </summary>
public static class SkillCatalog
{
    /// <summary>gradePoints is always aSkillGradeNum1 + aSkillGradeNum2 at every real call site, never one alone.</summary>
    public static float ReturnSkillValue(SkillDefinition skill, int gradePoints, SkillValueKind kind)
    {
        if (gradePoints < 1)
            return 0f;

        if (!TryGetGrades(skill, out var grade0, out var grade1) || grade0 is null || grade1 is null)
            return 0f;

        var maxUpgradePoint = skill.Skill.MaxUpgradePoint;
        if (maxUpgradePoint <= 0)
            return 0f; // defensive: the C++ divides unconditionally here (UB on 0), never observed in real data.

        var minValue = ReadField(grade0, kind);
        var maxValue = ReadField(grade1, kind);
        var value = minValue + (maxValue - minValue) * gradePoints / (float)maxUpgradePoint;

        if (value <= 0f) return 0f;
        return value < 1f ? 1f : value;
    }

    private static bool TryGetGrades(SkillDefinition skill, out SkillGradeRowDto? grade0,
        out SkillGradeRowDto? grade1)
    {
        grade0 = null;
        grade1 = null;

        foreach (var grade in skill.Grades)
            if (grade.GradeIndex == 0) grade0 = grade;
            else if (grade.GradeIndex == 1) grade1 = grade;

        return grade0 is not null && grade1 is not null;
    }

    private static int ReadField(SkillGradeRowDto grade, SkillValueKind kind)
    {
        return kind switch
        {
            SkillValueKind.ManaUse => grade.ManaUse,
            SkillValueKind.RecoverInfo1 => grade.RecoverInfo1,
            SkillValueKind.RecoverInfo2 => grade.RecoverInfo2,
            SkillValueKind.StunAttack => grade.StunAttack,
            SkillValueKind.StunDefense => grade.StunDefense,
            SkillValueKind.FastRunSpeed => grade.FastRunSpeed,
            SkillValueKind.AttackPowerRatio => grade.AttackInfo1,
            SkillValueKind.ElementAttackPowerRatio => grade.AttackInfo2,
            SkillValueKind.AttackInfo3 => grade.AttackInfo3,
            SkillValueKind.RunTime => grade.RunTime,
            SkillValueKind.ChargingDamageUp => grade.ChargingDamageUp,
            SkillValueKind.AttackPowerUp => grade.AttackPowerUp,
            SkillValueKind.DefensePowerUp => grade.DefensePowerUp,
            SkillValueKind.AttackSuccessUp => grade.AttackSuccessUp,
            SkillValueKind.AttackBlockUp => grade.AttackBlockUp,
            SkillValueKind.ElementAttackUp => grade.ElementAttackUp,
            SkillValueKind.ElementDefenseUp => grade.ElementDefenseUp,
            SkillValueKind.AttackSpeedUp => grade.AttackSpeedUp,
            SkillValueKind.RunSpeedUp => grade.RunSpeedUp,
            SkillValueKind.ShieldLifeUp => grade.ShieldLifeUp,
            SkillValueKind.LuckUp => grade.LuckUp,
            SkillValueKind.CriticalUp => grade.CriticalUp,
            SkillValueKind.ReturnSuccessUp => grade.ReturnSuccessUp,
            SkillValueKind.StunDefenseUp => grade.StunDefenseUp,
            SkillValueKind.DestroySuccessUp => grade.DestroySuccessUp,
            _ => 0
        };
    }
}
