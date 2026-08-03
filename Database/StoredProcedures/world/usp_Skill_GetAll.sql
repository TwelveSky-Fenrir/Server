CREATE PROCEDURE world.usp_Skill_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT SkillId,
           Name,
           Type,
           AttackType,
           DataNumber2D,
           TribeInfo1,
           TribeInfo2,
           LearnSkillPoint,
           MaxUpgradePoint,
           TotalHitNumber,
           ValidRadius
    FROM world.Skills
    ORDER BY SkillId;

    SELECT SkillId,
           LineIndex,
           Text
    FROM world.SkillDescriptions
    ORDER BY SkillId, LineIndex;

    SELECT SkillId,
           GradeIndex,
           ManaUse,
           RecoverInfo1,
           RecoverInfo2,
           StunAttack,
           StunDefense,
           FastRunSpeed,
           AttackInfo1,
           AttackInfo2,
           AttackInfo3,
           RunTime,
           ChargingDamageUp,
           AttackPowerUp,
           DefensePowerUp,
           AttackSuccessUp,
           AttackBlockUp,
           ElementAttackUp,
           ElementDefenseUp,
           AttackSpeedUp,
           RunSpeedUp,
           ShieldLifeUp,
           LuckUp,
           CriticalUp,
           ReturnSuccessUp,
           StunDefenseUp,
           DestroySuccessUp
    FROM world.SkillGrades
    ORDER BY SkillId, GradeIndex;
END;
