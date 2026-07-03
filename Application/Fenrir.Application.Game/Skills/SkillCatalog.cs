using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Pure C# port of <c>SKILLSYSTEM::ReturnSkillValue</c> (verified in full,
///     <c>Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:60-196</c>): linear interpolation between a
///     skill's two <c>world.SkillGrades</c> rows (GradeIndex 0 = min, 1 = max) by the caster's invested grade
///     points, out of the skill's own <c>MaxUpgradePoint</c> ceiling. No I/O -- every input is already-resolved
///     <see cref="SkillDefinition" /> data from <see cref="WorldDataCache" />.
/// </summary>
public static class SkillCatalog
{
    /// <summary>
    ///     <c>gradePoints</c> is the legacy's <c>sPoint</c> -- ALWAYS <c>aSkillGradeNum1 + aSkillGradeNum2</c> at
    ///     every real call site (report 04/05/12), never one alone. Returns 0 for <c>gradePoints &lt; 1</c> (the
    ///     source's own guard) and for an unresolvable grade pair (missing catalog data -- a defensive addition:
    ///     the C++ has no such guard because <c>Search()</c> can only ever return a fully-populated row).
    /// </summary>
    public static float ReturnSkillValue(SkillDefinition skill, int gradePoints, SkillValueKind kind)
    {
        if (gradePoints < 1)
            return 0f;

        if (!TryGetGrades(skill, out var grade0, out var grade1) || grade0 is null || grade1 is null)
            return 0f;

        var maxUpgradePoint = skill.Skill.MaxUpgradePoint;
        if (maxUpgradePoint <= 0)
            return 0f; // defensive: the C++ divides unconditionally (UB on 0), never observed in real data.

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
        {
            if (grade.GradeIndex == 0) grade0 = grade;
            else if (grade.GradeIndex == 1) grade1 = grade;
        }

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
