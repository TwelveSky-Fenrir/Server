namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

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
