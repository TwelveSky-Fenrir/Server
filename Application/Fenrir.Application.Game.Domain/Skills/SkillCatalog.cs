using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Port of SKILLSYSTEM::ReturnSkillValue: linear interpolation between a skill's two SkillGrades rows
///     (GradeIndex 0 = min, 1 = max) by the caster's invested grade points, out of MaxUpgradePoint.
/// </summary>
public static class SkillCatalog
{
    /// <summary>
    ///     <paramref name="gradePoints" /> is the legacy <c>sPoint</c> argument. Which grade the caller must
    ///     pass depends on what it is computing, and the two are NOT interchangeable:
    ///     <list type="bullet">
    ///         <item>
    ///             EFFECT values (buff magnitude/duration, targeted heal amount, attack ratio -- every factor
    ///             except <see cref="SkillValueKind.ManaUse" />) use the COMBINED grade
    ///             <c>aSkillGradeNum1 + aSkillGradeNum2</c> (invested points plus the item-granted
    ///             <c>GetBonusSkillValue</c>). Réf. C++ : <c>ProcessForCreateBuff</c>,
    ///             Server/ts25zone/S07_MyGame03.cpp:9328-9618.
    ///         </item>
    ///         <item>
    ///             The <see cref="SkillValueKind.ManaUse" /> cost uses the INVESTED grade
    ///             <c>aSkillGradeNum1</c> ALONE -- never the item bonus. Réf. C++ :
    ///             Server/ts25zone/S04_MyWork02.cpp:1640 (op15 skill-cast charge) and
    ///             Server/ts25zone/S07_MyGame04.cpp:2463 (auto-hunt charge), both passing
    ///             <c>aSkillGradeNum1</c> alone to factor 1. This method cannot enforce that split itself; the
    ///             caller (<c>Zone.ApplySkillCastManaCharge</c> / <c>Zone.ApplyCombatCommand</c>) owns it.
    ///         </item>
    ///     </list>
    /// </summary>
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
