-- Legacy MONSTER_INFO (Header/Protocol/STRUCT.h:156-204), 005_00004.IMG via MonsterReader. Real data
-- inspection (10000 fixed array slots) shows only 1139 slots have Index != 0 -- a dense front block
-- (1..~1139) plus a handful of hand-placed "event boss" ids scattered up to 9002 (e.g. 731 Santa, 9001
-- mini-boss, per ServerDocs/12_ts25zone/14_MyGame05.md); the other slots are reserved/unused, so (like
-- Items) we seed only the populated rows, keyed by the real legacy mIndex (never re-assigned).
--
-- Dense, always-meaningful scalar fields are plain columns. Two groups were deliberately NOT put here
-- (normalize, don't transliterate -- "il ne faut pas betement copier la logique du code Legacy"):
--   * mFrameInfo[6]/mHitFrame[3]/mSkillHitFrame[3]/mBulletInfo[2] are client animation/rendering data,
--     not server gameplay logic -- split into world.MonsterAnimationFrames (still 1 row per monster,
--     just physically separated so this table stays about gameplay stats).
--   * mDropMoneyInfo[3]/mDropPotionInfo[5][2]/mDropItemInfo[12]/mDropQuestItemInfo[2]/
--     mDropExtraItemInfo[50][2] are the loot tables -- see world.MonsterDrop* child tables. Those arrays
--     are genuinely sparse (e.g. only 13686 of 56950 possible extra-item slots are populated) so they
--     are normalized to one-row-per-populated-slot child tables, unlike the small dense arrays kept
--     inline below.
--
-- Kept inline (not split out) because always-populated and small: Size[4] (collision gabarit),
-- RadiusInfo[2] (detection radius), FollowInfo[2], SummonTime[2] (respawn timing) are small (<=4 slot),
-- always-meaningful arrays, same category as the ITEM_INFO.iEquipInfo[2] example called out in the brief.
--
-- Type/SpecialType/DamageType/AttackType/SizeCategory/CheckCollision are TINYINT: ServerDocs/12_ts25zone/
-- 20_GameSystem_Monster_Npc_Quest_Pet_Socket.md documents S15_MyShare.cpp field-bounds validation
-- (mType 1-15, mSpecialType 1-53, mDamageType 1-2) and real data confirms AttackType 1-6/CheckCollision
-- 1-2/SizeCategory 1-4 -- true bounded enums, unlike the wide stat fields (kept INT to match the legacy
-- int32 width exactly, since several -- Life, AttackPower, AttackSuccess -- observed up to the hundreds
-- of thousands/billions and would truncate in a narrower type).
--
-- No UNIQUE constraint on Name: real data has 144 monster names reused 2+ times (level/gear variant
-- reskins, e.g. "Green Deity[G9]".."[G12]"), so duplicates are expected, not a data-quality bug.
-- ChatLine1/2 are NULLable: an empty legacy mChatInfo buffer means "this monster says nothing" (755 of
-- 1139 monsters), which is "no value", not a real zero-length string -- same empty-buffer-to-NULL
-- convention as world.Items.Description1-3.
CREATE TABLE world.Monsters
(
    MonsterId           INT      NOT NULL,
    Name                NVARCHAR(25)  NOT NULL, -- mName[25] buffer size (legacy fixed-width, not a truncation limit we invented); never empty in real data
    ChatLine1           NVARCHAR(101) NULL,     -- mChatInfo[0], aggro/taunt line (populated for ~1/3 of monsters)
    ChatLine2           NVARCHAR(101) NULL,     -- mChatInfo[1], death/defeat line
    Type                TINYINT  NOT NULL,
    SpecialType         TINYINT  NOT NULL,
    DamageType          TINYINT  NOT NULL,
    DataSortNumber      SMALLINT NOT NULL,      -- client-side catalog sort key, not a gameplay value
    Size1               SMALLINT NOT NULL,
    Size2               SMALLINT NOT NULL,
    Size3               SMALLINT NOT NULL,
    Size4               SMALLINT NOT NULL,
    SizeCategory        TINYINT  NOT NULL,
    CheckCollision      TINYINT  NOT NULL,
    TotalHitNum         TINYINT  NOT NULL,
    TotalSkillHitNum    TINYINT  NOT NULL,
    ItemLevel           SMALLINT NOT NULL,
    MartialItemLevel    SMALLINT NOT NULL,
    RealLevel           SMALLINT NOT NULL,
    MartialRealLevel    SMALLINT NOT NULL,
    GeneralExperience   INT      NOT NULL,
    PatExperience       INT      NOT NULL,
    Life                INT      NOT NULL,      -- observed up to 1.3B (raid-boss HP pools), needs full INT width
    AttackType          TINYINT  NOT NULL,
    RadiusInfo1         SMALLINT NOT NULL,
    RadiusInfo2         SMALLINT NOT NULL,
    WalkSpeed           SMALLINT NOT NULL,
    RunSpeed            SMALLINT NOT NULL,
    DeathSpeed          SMALLINT NOT NULL,
    AttackPower         INT      NOT NULL,      -- observed up to 200000 -- a large-scale internal unit, not literal damage points
    DefensePower        INT      NOT NULL,
    AttackSuccess       INT      NOT NULL,
    AttackBlock         INT      NOT NULL,
    ElementAttackPower  INT      NOT NULL,
    ElementDefensePower INT      NOT NULL,
    Critical            SMALLINT NOT NULL,
    FollowInfo1         SMALLINT NOT NULL,
    FollowInfo2         SMALLINT NOT NULL,
    SummonTime1         INT      NOT NULL,      -- seconds; observed up to 86400 (24h) so needs more than SMALLINT
    SummonTime2         INT      NOT NULL,
    CONSTRAINT PK_Monsters PRIMARY KEY CLUSTERED (MonsterId)
);
