-- Contract: no parameters -> RS0, one row per world.Monsters row (1139 rows), INNER JOINed against
-- world.MonsterAnimationFrames (a 1:1 extension, always exactly one row per monster -- see that table's
-- comment -- so the join can never drop or duplicate a monster row).
-- Called once at GameServer boot to populate an in-memory cache (e.g. FrozenDictionary<int, Monster>
-- keyed by MonsterId) -- world.* is read-mostly reference data, never queried per-game-tick, so a plain
-- full-table SELECT is correct here, not a per-request lookup proc.
-- Ordered by MonsterId (the legacy mIndex) for deterministic, cache-friendly load order.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Monster_GetAll
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
ORDER BY m.MonsterId;
END;
