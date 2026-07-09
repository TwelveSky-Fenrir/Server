-- Monster static-data load-time field validation (legacy Load_Monster / Monster_CheckValidElement audit,
-- fenrir-database-engineer behavior contract "Monster static-data load-time validation
-- (Monster_CheckValidElement / Load_Monster)"). Legacy validates every populated slot of the 10000-slot
-- MONSTER_INFO array field-by-field before the shared-memory monster table is ever published to any other
-- process (Server/Header/S15_MyShare.cpp:1500-1833, MyShm::Monster_CheckValidElement; :565-605,
-- MyShm::Load_Monster -- first-offending-slot abort, no skip-and-continue), and that failure is fatal to
-- whichever process performed the load (Server/ts25sharemem/main.cpp:73-79;
-- Server/ts25zone/GameSystem/GameSystem_04_Monster.cpp:10-19 ->
-- Server/ts25zone/GameSystem/GameSystem_00_Load.cpp:37-41 -> Server/ts25zone/S01_MainApplication.cpp:277-282).
--
-- Today's world.Monsters / world.MonsterAnimationFrames / world.MonsterDrop* schema enforces none of this at
-- the storage level -- confirmed by direct inspection: Tables/world/Monsters.sql and
-- Tables/world/MonsterAnimationFrames.sql carry NOT NULL only, zero CHECK constraints of any kind, and the
-- five MonsterDrop* child tables (MonsterDropMoney/MonsterDropPotions/MonsterDropCategoryRates/
-- MonsterDropExtraItems/MonsterDropQuestItems) have only *structural* array-bound CHECKs on their SlotIndex/
-- CategoryIndex columns (verifying a slot position falls inside the array's original size) plus, for
-- MonsterDropMoney alone, the one cross-field ordering check (CK_MonsterDropMoney_AmountRange, MinAmount <=
-- MaxAmount) that already mirrors legacy. None of the four tables has ever had a value-range CHECK on the
-- rate/amount/identifier values themselves. This script adds exactly those missing value-range and
-- cross-field-ordering CHECK constraints, reproducing every single-field bound and cross-field-ordering rule
-- from Monster_CheckValidElement that is expressible as a row-scoped SQL CHECK.
--
-- New script, not an edit to the already-applied Tables/world/Monsters.sql / MonsterAnimationFrames.sql /
-- MonsterDropMoney.sql / MonsterDropPotions.sql / MonsterDropCategoryRates.sql / MonsterDropExtraItems.sql /
-- MonsterDropQuestItems.sql (DbMigrator journals every applied script by SHA-256 and refuses a changed
-- re-apply -- "never edit an already-applied script").
--
-- Deliberately NOT reproduced here (each citation is to the contract's own disposition of the item):
--   * MonsterId "must equal its own array slot position" (slot index + 1), including the associated
--     zero-slot-is-skipped edge case: MonsterId is Fenrir's PRIMARY KEY, boot-time/generator-assigned
--     reference data (currently populated 1-9002, sparse -- 1139 of 10000 legacy slots), not externally
--     supplied input, and the seed already contains only populated rows -- same reasoning
--     Migrations/031_quest_step_and_reward_amount_range_checks.sql applied to world.Quests.QuestId. A bare
--     "MonsterId BETWEEN 1 AND 10000" bound without the slot-position semantics would assert less than
--     legacy actually checked, so it is left out rather than shipped as a partial/misleading equivalent.
--   * The Thunder Giant (MonsterId 81) unconditional AttackType=1 load-time patch
--     (Server/Header/S15_MyShare.cpp:603-604): confirmed a no-op against the current seed
--     (Migrations/Seed/world/090_monsters.sql:262-263 already stores AttackType=1 for MonsterId 81), and a
--     CHECK constraint has no mechanism to *patch* a value the way the legacy loader's post-validation step
--     did -- nothing here would express it correctly, so it stays flagged as a data-import-tooling concern,
--     not a schema-constraint one.
--
-- Name/ChatLine1/ChatLine2 translation note: legacy's "must contain a null terminator within its N-byte
-- buffer" requirement (Server/Header/S15_MyShare.cpp:1519-1545) becomes, for a real NVARCHAR column with no
-- fixed-buffer/null-terminator concept, "stored content must be at most N-1 characters" -- world.Monsters.Name
-- is NVARCHAR(25) (legacy MAX_MONSTER_NAME_LENGTH = 25, Server/Header/Protocol/STRUCT.h:148) and
-- ChatLine1/ChatLine2 are NVARCHAR(101) (legacy MAX_MONSTER_DESCRIPTION_LENGTH = 101, STRUCT.h:150,160), so
-- the column's own declared width is exactly one character wider than legacy's own within-buffer rule permits.
--
-- Existing seed data (Migrations/Seed/world/090_monsters.sql, 1139 monsters / 274 potion slots / 13686 extra
-- item slots / 2130 category-rate slots / 24 quest-item rows) was checked field-by-field against every bound
-- below, including the three cross-field orderings (RadiusInfo2 >= RadiusInfo1, FollowInfo2 >= FollowInfo1,
-- SummonTime2 >= SummonTime1), before adding these as validating (WITH CHECK, the ALTER TABLE default)
-- constraints -- every seeded row is comfortably inside every bound below (several fields sit exactly at
-- their legacy upper bound, e.g. DefensePower/AttackSuccess/ElementAttackPower topping out at exactly
-- 100,000, FollowInfo2 topping out at exactly 100, and DropRate topping out at exactly 1,000,000 in more than
-- one table -- all still valid, since every bound below is inclusive), so this migration cannot fail against
-- the shipped dataset.

-- world.Monsters (Server/Header/S15_MyShare.cpp:1519-1787)
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Name CHECK (LEN(Name) <= 24); -- :1519-1530
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_ChatLines CHECK ( -- :1531-1545
        (ChatLine1 IS NULL OR LEN(ChatLine1) <= 100) AND
        (ChatLine2 IS NULL OR LEN(ChatLine2) <= 100)
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Type CHECK (Type BETWEEN 1 AND 15); -- :1546-1550
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_SpecialType CHECK (SpecialType BETWEEN 1 AND 53); -- :1551-1555
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_DamageType CHECK (DamageType BETWEEN 1 AND 2); -- :1556-1560
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_DataSortNumber CHECK (DataSortNumber BETWEEN 1 AND 10000); -- :1561-1565
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Size1To3 CHECK ( -- :1566-1573
        Size1 BETWEEN 1 AND 1000 AND
        Size2 BETWEEN 1 AND 1000 AND
        Size3 BETWEEN 1 AND 1000
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Size4 CHECK (Size4 BETWEEN 0 AND 1000); -- :1574-1578
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_SizeCategory CHECK (SizeCategory BETWEEN 1 AND 4); -- :1579-1583
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_CheckCollision CHECK (CheckCollision BETWEEN 1 AND 2); -- :1584-1588
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_HitCounts CHECK ( -- :1597-1601 (TotalHitNum), :1610-1614 (TotalSkillHitNum)
        TotalHitNum BETWEEN 0 AND 3 AND
        TotalSkillHitNum BETWEEN 0 AND 3
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_ItemLevel CHECK (ItemLevel BETWEEN 1 AND 145); -- :1648-1652, MAX_LIMIT_LEVEL_NUM
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_MartialItemLevel CHECK (MartialItemLevel BETWEEN 0 AND 25); -- :1653-1657
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_RealLevel CHECK (RealLevel BETWEEN 1 AND 1000); -- :1658-1662
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_MartialRealLevel CHECK (MartialRealLevel BETWEEN 0 AND 1000); -- :1663-1667
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_ExperienceRewards CHECK ( -- :1668-1672 (GeneralExperience), :1673-1677 (PatExperience)
        GeneralExperience BETWEEN 0 AND 100000000 AND
        PatExperience BETWEEN 0 AND 100000000
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Life CHECK (Life BETWEEN 1 AND 2000000000); -- :1678-1682, MAX_NUMBER_SIZE
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_AttackType CHECK (AttackType BETWEEN 1 AND 6); -- :1683-1687
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_RadiusInfo CHECK ( -- :1688-1692, :1693-1702 (incl. RadiusInfo2 >= RadiusInfo1)
        RadiusInfo1 BETWEEN 0 AND 10000 AND
        RadiusInfo2 BETWEEN 0 AND 10000 AND
        RadiusInfo2 >= RadiusInfo1
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_MovementSpeeds CHECK ( -- :1703-1717 (WalkSpeed/RunSpeed/DeathSpeed)
        WalkSpeed BETWEEN 0 AND 1000 AND
        RunSpeed BETWEEN 0 AND 1000 AND
        DeathSpeed BETWEEN 0 AND 1000
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_AttackPower CHECK (AttackPower BETWEEN 0 AND 1000000); -- :1718-1722
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_CombatStats CHECK ( -- :1723-1747 (DefensePower/AttackSuccess/AttackBlock/ElementAttackPower/ElementDefensePower)
        DefensePower BETWEEN 0 AND 100000 AND
        AttackSuccess BETWEEN 0 AND 100000 AND
        AttackBlock BETWEEN 0 AND 100000 AND
        ElementAttackPower BETWEEN 0 AND 100000 AND
        ElementDefensePower BETWEEN 0 AND 100000
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_Critical CHECK (Critical BETWEEN 0 AND 100); -- :1748-1752
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_FollowInfo CHECK ( -- :1753-1757, :1758-1767 (incl. FollowInfo2 >= FollowInfo1)
        FollowInfo1 BETWEEN 0 AND 100 AND
        FollowInfo2 BETWEEN 0 AND 100 AND
        FollowInfo2 >= FollowInfo1
        );
ALTER TABLE world.Monsters
    ADD CONSTRAINT CK_Monsters_SummonTime CHECK ( -- :1633-1637, :1638-1647 (incl. SummonTime2 >= SummonTime1)
        SummonTime1 BETWEEN 1 AND 1000000 AND
        SummonTime2 BETWEEN 1 AND 1000000 AND
        SummonTime2 >= SummonTime1
        );
GO

-- world.MonsterAnimationFrames (Server/Header/S15_MyShare.cpp:1589-1632) -- client rendering-only fields;
-- legacy validates them uniformly with everything else regardless of their lack of server-authoritative use
-- (see the table's own header comment), so this script does too.
ALTER TABLE world.MonsterAnimationFrames
    ADD CONSTRAINT CK_MonsterAnimationFrames_FrameInfo CHECK ( -- :1589-1596
        FrameInfo1 BETWEEN 1 AND 10000 AND
        FrameInfo2 BETWEEN 1 AND 10000 AND
        FrameInfo3 BETWEEN 1 AND 10000 AND
        FrameInfo4 BETWEEN 1 AND 10000 AND
        FrameInfo5 BETWEEN 1 AND 10000 AND
        FrameInfo6 BETWEEN 1 AND 10000
        );
ALTER TABLE world.MonsterAnimationFrames
    ADD CONSTRAINT CK_MonsterAnimationFrames_HitFrame CHECK ( -- :1602-1609
        HitFrame1 BETWEEN 0 AND 10000 AND
        HitFrame2 BETWEEN 0 AND 10000 AND
        HitFrame3 BETWEEN 0 AND 10000
        );
ALTER TABLE world.MonsterAnimationFrames
    ADD CONSTRAINT CK_MonsterAnimationFrames_SkillHitFrame CHECK ( -- :1615-1622
        SkillHitFrame1 BETWEEN 0 AND 10000 AND
        SkillHitFrame2 BETWEEN 0 AND 10000 AND
        SkillHitFrame3 BETWEEN 0 AND 10000
        );
ALTER TABLE world.MonsterAnimationFrames
    ADD CONSTRAINT CK_MonsterAnimationFrames_BulletInfo CHECK ( -- :1623-1632
        BulletInfo1 BETWEEN 1 AND 10000 AND
        BulletInfo2 BETWEEN 1 AND 10000
        );
GO

-- world.MonsterDropMoney (Server/Header/S15_MyShare.cpp:1768-1787) -- CK_MonsterDropMoney_AmountRange
-- (MinAmount <= MaxAmount) already exists on the original table script; only the three value-range bounds
-- are new here.
ALTER TABLE world.MonsterDropMoney
    ADD CONSTRAINT CK_MonsterDropMoney_DropRate CHECK (DropRate BETWEEN 0 AND 1000000); -- :1768-1772
ALTER TABLE world.MonsterDropMoney
    ADD CONSTRAINT CK_MonsterDropMoney_MinAmount CHECK (MinAmount BETWEEN 0 AND 100000000); -- :1773-1777
ALTER TABLE world.MonsterDropMoney
    ADD CONSTRAINT CK_MonsterDropMoney_MaxAmount CHECK (MaxAmount BETWEEN 0 AND 100000000); -- :1778-1787
GO

-- world.MonsterDropPotions (Server/Header/S15_MyShare.cpp:1788-1800) -- CK_MonsterDropPotions_SlotIndex
-- (0-4, structural array-bound) already exists; only the two value-range bounds are new here.
ALTER TABLE world.MonsterDropPotions
    ADD CONSTRAINT CK_MonsterDropPotions_DropRate CHECK (DropRate BETWEEN 0 AND 1000000); -- :1788-1794
ALTER TABLE world.MonsterDropPotions
    ADD CONSTRAINT CK_MonsterDropPotions_PotionItemId CHECK (PotionItemId BETWEEN 0 AND 99999); -- :1795-1800
GO

-- world.MonsterDropCategoryRates (Server/Header/S15_MyShare.cpp:1801-1808) --
-- CK_MonsterDropCategoryRates_CategoryIndex (0-11, structural array-bound) already exists; only the value
-- bound is new here.
ALTER TABLE world.MonsterDropCategoryRates
    ADD CONSTRAINT CK_MonsterDropCategoryRates_Value CHECK (Value BETWEEN 0 AND 1000000); -- :1801-1808
GO

-- world.MonsterDropExtraItems (Server/Header/S15_MyShare.cpp:1819-1830) --
-- CK_MonsterDropExtraItems_SlotIndex (0-49, structural array-bound) already exists; only the two
-- value-range bounds are new here. ItemId stays nullable-aware (rate-only slots are real seeded data --
-- see the table's own header comment).
ALTER TABLE world.MonsterDropExtraItems
    ADD CONSTRAINT CK_MonsterDropExtraItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000); -- :1819-1825
ALTER TABLE world.MonsterDropExtraItems
    ADD CONSTRAINT CK_MonsterDropExtraItems_ItemId CHECK (ItemId IS NULL OR ItemId BETWEEN 0 AND 99999); -- :1826-1830
GO

-- world.MonsterDropQuestItems (Server/Header/S15_MyShare.cpp:1809-1818) -- this table has never had any
-- CHECK constraint at all (single-row-per-monster shape, no SlotIndex to bound).
ALTER TABLE world.MonsterDropQuestItems
    ADD CONSTRAINT CK_MonsterDropQuestItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000); -- :1809-1813
ALTER TABLE world.MonsterDropQuestItems
    ADD CONSTRAINT CK_MonsterDropQuestItems_QuestItemId CHECK (QuestItemId BETWEEN 0 AND 99999); -- :1814-1818
GO
