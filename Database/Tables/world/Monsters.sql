-- Legacy MONSTER_INFO; only 1139 of 10000 slots have Index != 0 (dense front block plus scattered event-boss ids up to 9002), keyed by the real legacy mIndex.
-- Animation/rendering fields split into world.MonsterAnimationFrames; sparse loot-table arrays normalized into world.MonsterDrop* child tables.
-- ChatLine1/2 are NULL when the legacy mChatInfo buffer is empty (755/1139 monsters), same empty-buffer-to-NULL convention as world.Items.Description1-3.
CREATE TABLE world.Monsters
(
    MonsterId           INT           NOT NULL,
    Name                NVARCHAR(25)  NOT NULL,
    ChatLine1           NVARCHAR(101) NULL,     -- mChatInfo[0], aggro/taunt line
    ChatLine2           NVARCHAR(101) NULL,     -- mChatInfo[1], death/defeat line
    Type                TINYINT       NOT NULL,
    SpecialType         TINYINT       NOT NULL,
    DamageType          TINYINT       NOT NULL,
    DataSortNumber      SMALLINT      NOT NULL, -- client-side catalog sort key, not a gameplay value
    Size1               SMALLINT      NOT NULL,
    Size2               SMALLINT      NOT NULL,
    Size3               SMALLINT      NOT NULL,
    Size4               SMALLINT      NOT NULL,
    SizeCategory        TINYINT       NOT NULL,
    CheckCollision      TINYINT       NOT NULL,
    TotalHitNum         TINYINT       NOT NULL,
    TotalSkillHitNum    TINYINT       NOT NULL,
    ItemLevel           SMALLINT      NOT NULL,
    MartialItemLevel    SMALLINT      NOT NULL,
    RealLevel           SMALLINT      NOT NULL,
    MartialRealLevel    SMALLINT      NOT NULL,
    GeneralExperience   INT           NOT NULL,
    PatExperience       INT           NOT NULL,
    Life                INT           NOT NULL, -- observed up to 1.3B (raid-boss HP pools), needs full INT width
    AttackType          TINYINT       NOT NULL,
    RadiusInfo1         SMALLINT      NOT NULL,
    RadiusInfo2         SMALLINT      NOT NULL,
    WalkSpeed           SMALLINT      NOT NULL,
    RunSpeed            SMALLINT      NOT NULL,
    DeathSpeed          SMALLINT      NOT NULL,
    AttackPower         INT           NOT NULL, -- observed up to 200000 -- a large-scale internal unit, not literal damage points
    DefensePower        INT           NOT NULL,
    AttackSuccess       INT           NOT NULL,
    AttackBlock         INT           NOT NULL,
    ElementAttackPower  INT           NOT NULL,
    ElementDefensePower INT           NOT NULL,
    Critical            SMALLINT      NOT NULL,
    FollowInfo1         SMALLINT      NOT NULL,
    FollowInfo2         SMALLINT      NOT NULL,
    SummonTime1         INT           NOT NULL, -- seconds; observed up to 86400 (24h) so needs more than SMALLINT
    SummonTime2         INT           NOT NULL,
    CONSTRAINT PK_Monsters PRIMARY KEY CLUSTERED (MonsterId)
);
