-- Hardening (procs-world, moyenne): the FROM clause was originally an INNER JOIN to
-- world.MonsterAnimationFrames -- the FK only constrains MonsterAnimationFrames.MonsterId ->
-- Monsters.MonsterId, never the reverse, so nothing schema-enforces that every Monsters row has a matching
-- animation-frame sibling. A future monster added without one would have silently vanished from this ENTIRE
-- result set -- including its gameplay-critical Life/AttackPower/DefensePower/etc -- purely because a
-- cosmetic-only sibling row was missing, with no error anywhere in the pipeline. Now a LEFT JOIN, so a
-- missing sibling can never drop the monster row itself; each f.* column is ISNULL-defaulted (same idiom as
-- game.usp_Character_GetForWorldEntry's LEFT JOIN to game.CharacterQuests) so MonsterRowDto stays
-- non-nullable and Application/Fenrir.Application.Game.GameData/WorldDataCacheBuilder.cs's existing
-- range validation needs no change.
--
-- The defaults are NOT interchangeable cosmetic filler for every one of these columns -- verified against
-- actual consumers before picking them:
--   * FrameInfo1-6 ARE server-authoritative in Fenrir's own reimplementation despite
--     world.MonsterAnimationFrames's own header comment saying "no server-authoritative use" (true for
--     legacy ts25zone, not true here): Fenrir's MonsterAiSystem
--     (Application/Fenrir.Application.Game.Domain/World/Monsters/MonsterAiSystem.cs:53,79,89,99,109) and
--     MonsterDeathSequence (.../MonsterDeathSequence.cs:27) read Template.FrameInfo1-6 as the AI
--     state-machine's Spawning/AttackWindup/RangedAttackWindup/Flinch/Dead/ReturnToSpawn duration knobs,
--     always through a `Math.Max(1, (int)...)` floor. Defaulting a missing value to 1 (the same floor the
--     consumer already applies, and the CK_MonsterAnimationFrames_FrameInfo/BulletInfo lower bound) yields
--     the fastest-possible, always-safe state transition rather than a stall or a crash -- never invent a
--     larger "plausible" duration here, 1 is the only default consistent with both the CHECK constraint and
--     the consumer's own defensive clamp.
--   * HitFrame1-3/SkillHitFrame1-3/BulletInfo1-2 have zero consumer beyond
--     WorldDataCacheBuilder's own boot-time CHECK-mirroring range validation (verified: no Application-layer
--     read of these six columns anywhere outside that validation) -- true cosmetic filler, defaulted to
--     their own CHECK constraint's lower bound (0 for HitFrame/SkillHitFrame, 1 for BulletInfo) purely to
--     stay inside the bound WorldDataCacheBuilder already checks, not because any code reads the value.
CREATE OR ALTER PROCEDURE world.usp_Monster_GetAll
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
           ISNULL(f.FrameInfo1, 1)      AS FrameInfo1,
           ISNULL(f.FrameInfo2, 1)      AS FrameInfo2,
           ISNULL(f.FrameInfo3, 1)      AS FrameInfo3,
           ISNULL(f.FrameInfo4, 1)      AS FrameInfo4,
           ISNULL(f.FrameInfo5, 1)      AS FrameInfo5,
           ISNULL(f.FrameInfo6, 1)      AS FrameInfo6,
           ISNULL(f.HitFrame1, 0)       AS HitFrame1,
           ISNULL(f.HitFrame2, 0)       AS HitFrame2,
           ISNULL(f.HitFrame3, 0)       AS HitFrame3,
           ISNULL(f.SkillHitFrame1, 0)  AS SkillHitFrame1,
           ISNULL(f.SkillHitFrame2, 0)  AS SkillHitFrame2,
           ISNULL(f.SkillHitFrame3, 0)  AS SkillHitFrame3,
           ISNULL(f.BulletInfo1, 1)     AS BulletInfo1,
           ISNULL(f.BulletInfo2, 1)     AS BulletInfo2
    FROM world.Monsters AS m
             LEFT JOIN world.MonsterAnimationFrames AS f ON f.MonsterId = m.MonsterId
    ORDER BY m.MonsterId;
END;
