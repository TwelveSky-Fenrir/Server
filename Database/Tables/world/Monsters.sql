-- Legacy MONSTER_INFO; only 1139 of 10000 slots have Index != 0 (dense front block plus scattered event-boss ids up to 9002), keyed by the real legacy mIndex.
-- Animation/rendering fields split into world.MonsterAnimationFrames; sparse loot-table arrays normalized into world.MonsterDrop* child tables.
-- ChatLine1/2 are NULL when the legacy mChatInfo buffer is empty (755/1139 monsters), same empty-buffer-to-NULL convention as world.Items.Description1-3.
--
-- The CHECKs below mirror legacy's load-time field validation (Monster_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:1500-1833), which validates every populated slot of the 10000-slot
-- MONSTER_INFO array field-by-field before the shared-memory monster table is ever published to any other
-- process, aborting the whole load on the first offending slot -- fatal to whichever process performed it.
-- Name/ChatLine1/ChatLine2 translate legacy's "must contain a null terminator within its N-byte buffer"
-- rule into, for a real NVARCHAR column with no fixed-buffer/null-terminator concept, "stored content must
-- be at most N-1 characters" (Name is NVARCHAR(25) = legacy MAX_MONSTER_NAME_LENGTH,
-- Server/Header/Protocol/STRUCT.h:148; ChatLine1/2 are NVARCHAR(101) = legacy
-- MAX_MONSTER_DESCRIPTION_LENGTH, STRUCT.h:150,160 -- one character wider than legacy's own within-buffer
-- rule permits). MonsterId's "must equal its own array slot position" rule is deliberately NOT reproduced:
-- MonsterId is Fenrir's PRIMARY KEY, boot-time/generator-assigned reference data (currently populated
-- 1-9002, sparse), not externally supplied input.
CREATE TABLE world.Monsters
(
    MonsterId           INT           NOT NULL,
    Name                NVARCHAR(25)  NOT NULL,
    ChatLine1           NVARCHAR(101) NULL,                                              -- mChatInfo[0], aggro/taunt line
    ChatLine2           NVARCHAR(101) NULL,                                              -- mChatInfo[1], death/defeat line
    Type                TINYINT       NOT NULL,
    SpecialType         TINYINT       NOT NULL,
    DamageType          TINYINT       NOT NULL,
    DataSortNumber      SMALLINT      NOT NULL,                                          -- client-side catalog sort key, not a gameplay value
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
    Life                INT           NOT NULL,                                          -- observed up to 1.3B (raid-boss HP pools), needs full INT width
    AttackType          TINYINT       NOT NULL,
    RadiusInfo1         SMALLINT      NOT NULL,
    RadiusInfo2         SMALLINT      NOT NULL,
    WalkSpeed           SMALLINT      NOT NULL,
    RunSpeed            SMALLINT      NOT NULL,
    DeathSpeed          SMALLINT      NOT NULL,
    AttackPower         INT           NOT NULL,                                          -- observed up to 200000 -- a large-scale internal unit, not literal damage points
    DefensePower        INT           NOT NULL,
    AttackSuccess       INT           NOT NULL,
    AttackBlock         INT           NOT NULL,
    ElementAttackPower  INT           NOT NULL,
    ElementDefensePower INT           NOT NULL,
    Critical            SMALLINT      NOT NULL,
    FollowInfo1         SMALLINT      NOT NULL,
    FollowInfo2         SMALLINT      NOT NULL,
    SummonTime1         INT           NOT NULL,                                          -- seconds; observed up to 86400 (24h) so needs more than SMALLINT
    SummonTime2         INT           NOT NULL,
    CONSTRAINT PK_Monsters PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT CK_Monsters_Name CHECK (LEN(Name) <= 24),                                 -- Server/Header/S15_MyShare.cpp:1519-1530
    CONSTRAINT CK_Monsters_ChatLines CHECK (                                             -- :1531-1545
        (ChatLine1 IS NULL OR LEN(ChatLine1) <= 100) AND
        (ChatLine2 IS NULL OR LEN(ChatLine2) <= 100)
        ),
    CONSTRAINT CK_Monsters_Type CHECK (Type BETWEEN 1 AND 15),                           -- :1546-1550
    CONSTRAINT CK_Monsters_SpecialType CHECK (SpecialType BETWEEN 1 AND 53),             -- :1551-1555
    CONSTRAINT CK_Monsters_DamageType CHECK (DamageType BETWEEN 1 AND 2),                -- :1556-1560
    CONSTRAINT CK_Monsters_DataSortNumber CHECK (DataSortNumber BETWEEN 1 AND 10000),    -- :1561-1565
    CONSTRAINT CK_Monsters_Size1To3 CHECK (                                              -- :1566-1573
        Size1 BETWEEN 1 AND 1000 AND
        Size2 BETWEEN 1 AND 1000 AND
        Size3 BETWEEN 1 AND 1000
        ),
    CONSTRAINT CK_Monsters_Size4 CHECK (Size4 BETWEEN 0 AND 1000),                       -- :1574-1578
    CONSTRAINT CK_Monsters_SizeCategory CHECK (SizeCategory BETWEEN 1 AND 4),            -- :1579-1583
    CONSTRAINT CK_Monsters_CheckCollision CHECK (CheckCollision BETWEEN 1 AND 2),        -- :1584-1588
    CONSTRAINT CK_Monsters_HitCounts CHECK (                                             -- :1597-1601 (TotalHitNum), :1610-1614 (TotalSkillHitNum)
        TotalHitNum BETWEEN 0 AND 3 AND
        TotalSkillHitNum BETWEEN 0 AND 3
        ),
    CONSTRAINT CK_Monsters_ItemLevel CHECK (ItemLevel BETWEEN 1 AND 145),                -- :1648-1652, MAX_LIMIT_LEVEL_NUM
    CONSTRAINT CK_Monsters_MartialItemLevel CHECK (MartialItemLevel BETWEEN 0 AND 25),   -- :1653-1657
    CONSTRAINT CK_Monsters_RealLevel CHECK (RealLevel BETWEEN 1 AND 1000),               -- :1658-1662
    CONSTRAINT CK_Monsters_MartialRealLevel CHECK (MartialRealLevel BETWEEN 0 AND 1000), -- :1663-1667
    CONSTRAINT CK_Monsters_ExperienceRewards CHECK (                                     -- :1668-1672 (GeneralExperience), :1673-1677 (PatExperience)
        GeneralExperience BETWEEN 0 AND 100000000 AND
        PatExperience BETWEEN 0 AND 100000000
        ),
    CONSTRAINT CK_Monsters_Life CHECK (Life BETWEEN 1 AND 2000000000),                   -- :1678-1682, MAX_NUMBER_SIZE
    CONSTRAINT CK_Monsters_AttackType CHECK (AttackType BETWEEN 1 AND 6),                -- :1683-1687
    CONSTRAINT CK_Monsters_RadiusInfo CHECK (                                            -- :1688-1692, :1693-1702 (incl. RadiusInfo2 >= RadiusInfo1)
        RadiusInfo1 BETWEEN 0 AND 10000 AND
        RadiusInfo2 BETWEEN 0 AND 10000 AND
        RadiusInfo2 >= RadiusInfo1
        ),
    CONSTRAINT CK_Monsters_MovementSpeeds CHECK (                                        -- :1703-1717 (WalkSpeed/RunSpeed/DeathSpeed)
        WalkSpeed BETWEEN 0 AND 1000 AND
        RunSpeed BETWEEN 0 AND 1000 AND
        DeathSpeed BETWEEN 0 AND 1000
        ),
    CONSTRAINT CK_Monsters_AttackPower CHECK (AttackPower BETWEEN 0 AND 1000000),        -- :1718-1722
    CONSTRAINT CK_Monsters_CombatStats CHECK (                                           -- :1723-1747 (DefensePower/AttackSuccess/AttackBlock/ElementAttackPower/ElementDefensePower)
        DefensePower BETWEEN 0 AND 100000 AND
        AttackSuccess BETWEEN 0 AND 100000 AND
        AttackBlock BETWEEN 0 AND 100000 AND
        ElementAttackPower BETWEEN 0 AND 100000 AND
        ElementDefensePower BETWEEN 0 AND 100000
        ),
    CONSTRAINT CK_Monsters_Critical CHECK (Critical BETWEEN 0 AND 100),                  -- :1748-1752
    CONSTRAINT CK_Monsters_FollowInfo CHECK (                                            -- :1753-1757, :1758-1767 (incl. FollowInfo2 >= FollowInfo1)
        FollowInfo1 BETWEEN 0 AND 100 AND
        FollowInfo2 BETWEEN 0 AND 100 AND
        FollowInfo2 >= FollowInfo1
        ),
    CONSTRAINT CK_Monsters_SummonTime CHECK (                                            -- :1633-1637, :1638-1647 (incl. SummonTime2 >= SummonTime1)
        SummonTime1 BETWEEN 1 AND 1000000 AND
        SummonTime2 BETWEEN 1 AND 1000000 AND
        SummonTime2 >= SummonTime1
        )
);
