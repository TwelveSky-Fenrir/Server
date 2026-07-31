using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

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

[GenerateDto]
public sealed partial record SkillDescriptionRowDto(
    int SkillId,
    byte LineIndex,
    string Text);

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
