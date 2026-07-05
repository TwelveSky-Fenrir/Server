-- Client animation/rendering fields split out of world.Monsters (mFrameInfo/mHitFrame/mSkillHitFrame/mBulletInfo), 1:1 per monster.
-- No server-authoritative use; kept only so migrated data stays lossless for future client-facing tooling.
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
