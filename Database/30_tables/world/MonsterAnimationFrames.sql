-- Client animation/rendering data split out of world.Monsters (mFrameInfo[6]/mHitFrame[3]/
-- mSkillHitFrame[3]/mBulletInfo[2], Header/Protocol/STRUCT.h:156-204): a 1:1 extension, always exactly
-- one row per monster (frame indices are dense, not sparse, so this is a physical decluttering of the
-- parent table, not a normalization of a sparse array like the drop tables). GameServer has no server-
-- authoritative use for these -- they exist purely so the migrated data stays complete/lossless for any
-- future client-facing tooling that wants to replay legacy animation timings.
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
    CONSTRAINT FK_MonsterAnimationFrames_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId)
);
