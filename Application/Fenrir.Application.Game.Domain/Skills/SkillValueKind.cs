namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     The sFactor argument of the legacy SKILLSYSTEM::ReturnSkillValue(sIndex, sPoint, sFactor) -- selects
///     which pair of world.SkillGrades columns (grade 0 = min, grade 1 = max) to interpolate between.
/// </summary>
public enum SkillValueKind
{
    ManaUse = 1,

    /// <summary>Meditation regen-per-tick divisor, or a targeted heal's flat HP amount -- same column, two meanings.</summary>
    RecoverInfo1 = 2,

    /// <summary>Same dual-interpretation as RecoverInfo1, for MP.</summary>
    RecoverInfo2 = 3,

    StunAttack = 4,
    StunDefense = 5,
    FastRunSpeed = 6,
    AttackPowerRatio = 7,
    ElementAttackPowerRatio = 8,
    AttackInfo3 = 9,

    /// <summary>Buff duration in legacy ticks.</summary>
    RunTime = 10,

    ChargingDamageUp = 11,
    AttackPowerUp = 12,
    DefensePowerUp = 13,
    AttackSuccessUp = 14,
    AttackBlockUp = 15,
    ElementAttackUp = 16,
    ElementDefenseUp = 17,
    AttackSpeedUp = 18,
    RunSpeedUp = 19,

    /// <summary>Holy Shield value = ratio% x caster MaxLife x 0.01.</summary>
    ShieldLifeUp = 20,

    LuckUp = 21,
    CriticalUp = 22,
    ReturnSuccessUp = 23,
    StunDefenseUp = 24,
    DestroySuccessUp = 25
}
