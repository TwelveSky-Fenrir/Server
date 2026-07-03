using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Skills row -- ordinal contract of world.usp_Skill_GetAll's RS0 (153 rows). Constructor order
///     must track the SELECT column order exactly (invariant I-04); [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record SkillRowDto(
    int SkillId,
    string Name,
    byte Type,
    byte AttackType,
    short DataNumber2D,
    byte TribeInfo1,
    byte TribeInfo2,
    byte LearnSkillPoint,
    byte MaxUpgradePoint,
    byte TotalHitNumber,
    short ValidRadius);

/// <summary>One populated world.SkillDescriptions line -- world.usp_Skill_GetAll RS1 (LineIndex 0-9).</summary>
[GenerateDto]
public sealed partial record SkillDescriptionRowDto(
    int SkillId,
    byte LineIndex,
    string Text);

/// <summary>
///     One world.SkillGrades row -- world.usp_Skill_GetAll RS2 (exactly 2 rows per skill, GradeIndex 0/1:
///     the legacy base/upgraded grade pair).
/// </summary>
[GenerateDto]
public sealed partial record SkillGradeRowDto(
    int SkillId,
    byte GradeIndex,
    short ManaUse,
    byte RecoverInfo1,
    byte RecoverInfo2,
    byte StunAttack,
    byte StunDefense,
    short FastRunSpeed,
    short AttackInfo1,
    byte AttackInfo2,
    byte AttackInfo3,
    short RunTime,
    byte ChargingDamageUp,
    byte AttackPowerUp,
    byte DefensePowerUp,
    byte AttackSuccessUp,
    byte AttackBlockUp,
    byte ElementAttackUp,
    byte ElementDefenseUp,
    byte AttackSpeedUp,
    byte RunSpeedUp,
    byte ShieldLifeUp,
    byte LuckUp,
    byte CriticalUp,
    byte ReturnSuccessUp,
    byte StunDefenseUp,
    byte DestroySuccessUp);
