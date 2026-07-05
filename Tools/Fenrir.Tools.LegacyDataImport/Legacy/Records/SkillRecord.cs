namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of the legacy <c>GRADE_INFO_FOR_SKILL</c> struct
///     (Header/Protocol/STRUCT.h:95-121), 100 bytes / 25 ints, no padding.
/// </summary>
internal sealed record SkillGradeRecord(
    int ManaUse,
    int[] RecoverInfo,
    int StunAttack,
    int StunDefense,
    int FastRunSpeed,
    int[] AttackInfo,
    int RunTime,
    int ChargingDamageUp,
    int AttackPowerUp,
    int DefensePowerUp,
    int AttackSuccessUp,
    int AttackBlockUp,
    int ElementAttackUp,
    int ElementDefenseUp,
    int AttackSpeedUp,
    int RunSpeedUp,
    int ShieldLifeUp,
    int LuckUp,
    int CriticalUp,
    int ReturnSuccessUp,
    int StunDefenseUp,
    int DestroySuccessUp);

/// <summary>
///     Faithful in-memory shape of a legacy <c>SKILL_INFO</c> record (Header/Protocol/STRUCT.h:123-146),
///     776 bytes total, embedding <see cref="SkillGradeRecord" /> (<c>GRADE_INFO_FOR_SKILL</c>) x2.
/// </summary>
internal sealed record SkillRecord(
    int Index,
    string Name,
    string[] Description,
    int Type,
    int AttackType,
    int DataNumber2D,
    int[] TribeInfo,
    int LearnSkillPoint,
    int MaxUpgradePoint,
    int TotalHitNumber,
    int ValidRadius,
    SkillGradeRecord[] GradeInfo);
