namespace Fenrir.Application.Game.Skills;

/// <summary>
///     The <c>sFactor</c> argument of the legacy <c>SKILLSYSTEM::ReturnSkillValue(sIndex, sPoint, sFactor)</c>
///     (verified in full, <c>Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:60-196</c>) -- selects which
///     pair of <c>world.SkillGrades</c> columns (grade 0 = min, grade 1 = max) to interpolate between. Every
///     enum member cites the exact <see cref="Fenrir.Data.World.SkillGradeRowDto" /> field it reads so the
///     mapping is independently checkable against that DTO's own ordinal contract.
/// </summary>
public enum SkillValueKind
{
    /// <summary>1: <c>ManaUse</c> -- MP cost of casting/attacking with this skill.</summary>
    ManaUse = 1,

    /// <summary>
    ///     2: <c>RecoverInfo1</c> -- meditation HP-regen-per-tick DIVISOR (report 05 §7 pt.3) OR a targeted heal's flat
    ///     HP amount (skills 106/108/110) -- two different interpretations of the SAME column, both verified at their own call
    ///     sites; never conflate them.
    /// </summary>
    RecoverInfo1 = 2,

    /// <summary>
    ///     3: <c>RecoverInfo2</c> -- meditation MP-regen-per-tick divisor, or a targeted heal's flat MP amount (skills
    ///     107/109/111) -- same dual-interpretation caveat as <see cref="RecoverInfo1" />.
    /// </summary>
    RecoverInfo2 = 3,

    /// <summary>4: <c>StunAttack</c>.</summary>
    StunAttack = 4,

    /// <summary>5: <c>StunDefense</c>.</summary>
    StunDefense = 5,

    /// <summary>6: <c>FastRunSpeed</c>.</summary>
    FastRunSpeed = 6,

    /// <summary>
    ///     7: <c>AttackInfo1</c> -- the attack-power-up RATIO (percent) fed into <c>CalDamage</c> for a skill-based
    ///     attack (report 05 §4).
    /// </summary>
    AttackPowerRatio = 7,

    /// <summary>8: <c>AttackInfo2</c> -- the element-attack-power-up ratio (percent).</summary>
    ElementAttackPowerRatio = 8,

    /// <summary>9: <c>AttackInfo3</c>.</summary>
    AttackInfo3 = 9,

    /// <summary>
    ///     10: <c>RunTime</c> -- buff DURATION in legacy ticks (before the (unmodeled) <c>mSupportSkillTimeUpRatio</c>
    ///     multiplier -- report 12 §4.2).
    /// </summary>
    RunTime = 10,

    /// <summary>11: <c>ChargingDamageUp</c> -- writes buff slot 8 (skills 6/25/44).</summary>
    ChargingDamageUp = 11,

    /// <summary>12: <c>AttackPowerUp</c> -- writes buff slot 0 (skills 15/30/53, weapon-class gated).</summary>
    AttackPowerUp = 12,

    /// <summary>13: <c>DefensePowerUp</c> -- writes buff slot 1 (skills 11/34/49, weapon-class gated).</summary>
    DefensePowerUp = 13,

    /// <summary>14: <c>AttackSuccessUp</c> -- writes buff slot 2 (skill 76, party formation).</summary>
    AttackSuccessUp = 14,

    /// <summary>15: <c>AttackBlockUp</c> -- writes buff slot 3 (skills 19/38/57 weapon-gated, and skill 77 party).</summary>
    AttackBlockUp = 15,

    /// <summary>16: <c>ElementAttackUp</c> -- writes buff slot 4 (skills 7/26/45).</summary>
    ElementAttackUp = 16,

    /// <summary>17: <c>ElementDefenseUp</c> -- writes buff slot 6 (skills 7/26/45).</summary>
    ElementDefenseUp = 17,

    /// <summary>18: <c>AttackSpeedUp</c>.</summary>
    AttackSpeedUp = 18,

    /// <summary>19: <c>RunSpeedUp</c> -- writes buff slot 7 (skills 19/38/57).</summary>
    RunSpeedUp = 19,

    /// <summary>
    ///     20: <c>ShieldLifeUp</c> -- writes buff slot 9 (Holy Shield, skills 82/79), value = ratio% × caster MaxLife ×
    ///     0.01.
    /// </summary>
    ShieldLifeUp = 20,

    /// <summary>21: <c>LuckUp</c> -- writes buff slot 11 (skill 84).</summary>
    LuckUp = 21,

    /// <summary>22: <c>CriticalUp</c> -- writes buff slot 10 (skills 83/81).</summary>
    CriticalUp = 22,

    /// <summary>23: <c>ReturnSuccessUp</c> -- writes buff slot 12 (skill 103).</summary>
    ReturnSuccessUp = 23,

    /// <summary>24: <c>StunDefenseUp</c> -- writes buff slot 13 (skill 104).</summary>
    StunDefenseUp = 24,

    /// <summary>25: <c>DestroySuccessUp</c> -- writes buff slot 14 (skill 105).</summary>
    DestroySuccessUp = 25
}
