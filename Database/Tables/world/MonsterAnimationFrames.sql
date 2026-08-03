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
    CONSTRAINT CK_MonsterAnimationFrames_FrameInfo CHECK (
        FrameInfo1 BETWEEN 1 AND 10000 AND
        FrameInfo2 BETWEEN 1 AND 10000 AND
        FrameInfo3 BETWEEN 1 AND 10000 AND
        FrameInfo4 BETWEEN 1 AND 10000 AND
        FrameInfo5 BETWEEN 1 AND 10000 AND
        FrameInfo6 BETWEEN 1 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_HitFrame CHECK (
        HitFrame1 BETWEEN 0 AND 10000 AND
        HitFrame2 BETWEEN 0 AND 10000 AND
        HitFrame3 BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_SkillHitFrame CHECK (
        SkillHitFrame1 BETWEEN 0 AND 10000 AND
        SkillHitFrame2 BETWEEN 0 AND 10000 AND
        SkillHitFrame3 BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_MonsterAnimationFrames_BulletInfo CHECK (
        BulletInfo1 BETWEEN 1 AND 10000 AND
        BulletInfo2 BETWEEN 1 AND 10000
        )
);
