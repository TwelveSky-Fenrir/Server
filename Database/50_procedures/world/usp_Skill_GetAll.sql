-- Contract: no parameters -> 3 result sets, called once at GameServer boot to populate an in-memory
-- cache (world.* is read-mostly reference data, never queried per-game-tick -- one round trip, one
-- proc, no native compilation needed). All 3 sets are ordered by SkillId (RS1/RS2 then by the child
-- key) so the caller can zip them back together into one SkillId -> Skill+Descriptions+Grades shape
-- without a self-join that would otherwise duplicate the parent row per child (up to 7 description
-- lines x 2 grades per skill).
--   RS0: one row per world.Skills (153 rows)      -- SkillId, Name, Type, AttackType, DataNumber2D,
--                                                     TribeInfo1, TribeInfo2, LearnSkillPoint,
--                                                     MaxUpgradePoint, TotalHitNumber, ValidRadius
--   RS1: one row per populated world.SkillDescriptions line -- SkillId, LineIndex, Text
--   RS2: exactly 2 rows per world.SkillGrades (grade 0/1)   -- SkillId, GradeIndex, <22 grade fields>
-- Idempotent: yes (read-only). Errors: none.
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
