-- Client animation/rendering fields split out of world.Monsters (mFrameInfo/mHitFrame/mSkillHitFrame/mBulletInfo), 1:1 per monster.
-- Correction (verified against actual Fenrir consumers, not just legacy intent): FrameInfo1-6 are NOT purely
-- cosmetic in Fenrir's own reimplementation -- MonsterAiSystem
-- (Application/Fenrir.Application.Game.Domain/World/Monsters/MonsterAiSystem.cs:53,79,89,99,109) and
-- MonsterDeathSequence (.../MonsterDeathSequence.cs:27) read them as the AI state-machine's
-- Spawning/AttackWindup/RangedAttackWindup/Flinch/Dead/ReturnToSpawn duration knobs. Only HitFrame1-3/
-- SkillHitFrame1-3/BulletInfo1-2 have no server-authoritative consumer (verified: nothing outside
-- WorldDataCacheBuilder's own boot-time range validation reads them) and remain kept only so migrated data
-- stays lossless for future client-facing tooling. See world.usp_Monster_GetAll's own header comment for how
-- its LEFT JOIN now defaults each of these fourteen columns when this table has no matching row.
-- The CHECKs below mirror legacy's load-time field validation (Monster_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:1589-1632) -- legacy validates these client rendering-only fields uniformly
-- with everything else regardless of their lack of server-authoritative use, so this schema does too.
CREATE TABLE world.MonsterAnimationFrames
(
    MonsterId      INT      NOT NULL,
    FrameInfo1     SMALLINT NOT NULL,
    FrameInfo2     SMALLINT NOT NULL,
    FrameInfo3     SMALLINT NOT NULL,
    FrameInfo4     SMALLINT NOT NULL,
    FrameInfo5     SMALLINT NOT NULL,
    FrameInfo6     SMALLINT NOT NULL,
    HitFrame1      SMALLINT NOT NULL,
    HitFrame2      SMALLINT NOT NULL,
    HitFrame3      SMALLINT NOT NULL,
    SkillHitFrame1 SMALLINT NOT NULL,
    SkillHitFrame2 SMALLINT NOT NULL,
    SkillHitFrame3 SMALLINT NOT NULL,
    BulletInfo1    SMALLINT NOT NULL,
    BulletInfo2    SMALLINT NOT NULL,
    CONSTRAINT PK_MonsterAnimationFrames PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterAnimationFrames_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterAnimationFrames_FrameInfo CHECK (     -- Server/Header/S15_MyShare.cpp:1589-1596
        FrameInfo1 BETWEEN 1 AND 10000 AND
        FrameInfo2 BETWEEN 1 AND 10000 AND
        FrameInfo3 BETWEEN 1 AND 10000 AND
        FrameInfo4 BETWEEN 1 AND 10000 AND
        FrameInfo5 BETWEEN 1 AND 10000 AND
        FrameInfo6 BETWEEN 1 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_HitFrame CHECK (      -- :1602-1609
        HitFrame1 BETWEEN 0 AND 10000 AND
        HitFrame2 BETWEEN 0 AND 10000 AND
        HitFrame3 BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_SkillHitFrame CHECK ( -- :1615-1622
        SkillHitFrame1 BETWEEN 0 AND 10000 AND
        SkillHitFrame2 BETWEEN 0 AND 10000 AND
        SkillHitFrame3 BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_BulletInfo CHECK (    -- :1623-1632
        BulletInfo1 BETWEEN 1 AND 10000 AND
        BulletInfo2 BETWEEN 1 AND 10000
        )
);
