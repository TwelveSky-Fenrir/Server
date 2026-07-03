-- Contract: @MonsterId INT -> RS0, 0 or 1 row (the monster + its animation-frame extension row).
-- NOT part of the boot-time cache path (GameServer always loads the full catalog via
-- usp_Monster_GetAll) -- this is a tooling/debugging lookup (e.g. an admin console or ad-hoc data
-- inspection) so a single monster can be pulled without the app ever touching world.Monsters directly
-- (security model: no table/view grants exist, every access is via a procedure).
-- Idempotent: yes (read-only). Errors: none (empty result set if MonsterId does not exist).
CREATE PROCEDURE world.usp_Monster_GetById @MonsterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT m.MonsterId,
       m.Name,
       m.ChatLine1,
       m.ChatLine2,
       m.Type,
       m.SpecialType,
       m.DamageType,
       m.DataSortNumber,
       m.Size1,
       m.Size2,
       m.Size3,
       m.Size4,
       m.SizeCategory,
       m.CheckCollision,
       m.TotalHitNum,
       m.TotalSkillHitNum,
       m.ItemLevel,
       m.MartialItemLevel,
       m.RealLevel,
       m.MartialRealLevel,
       m.GeneralExperience,
       m.PatExperience,
       m.Life,
       m.AttackType,
       m.RadiusInfo1,
       m.RadiusInfo2,
       m.WalkSpeed,
       m.RunSpeed,
       m.DeathSpeed,
       m.AttackPower,
       m.DefensePower,
       m.AttackSuccess,
       m.AttackBlock,
       m.ElementAttackPower,
       m.ElementDefensePower,
       m.Critical,
       m.FollowInfo1,
       m.FollowInfo2,
       m.SummonTime1,
       m.SummonTime2,
       f.FrameInfo1,
       f.FrameInfo2,
       f.FrameInfo3,
       f.FrameInfo4,
       f.FrameInfo5,
       f.FrameInfo6,
       f.HitFrame1,
       f.HitFrame2,
       f.HitFrame3,
       f.SkillHitFrame1,
       f.SkillHitFrame2,
       f.SkillHitFrame3,
       f.BulletInfo1,
       f.BulletInfo2
FROM world.Monsters AS m
         INNER JOIN world.MonsterAnimationFrames AS f ON f.MonsterId = m.MonsterId
WHERE m.MonsterId = @MonsterId;
END;
