-- Legacy ITEM_INFO (post-runtime-patch data, i.e. what the live legacy server actually serves); one row per record where Index != 0.
-- GainSkillNumber is a nullable FK to world.Skills despite the "Number" name -- it's a skill reference, not a quantity (0 -> NULL). BonusSkillInfo[8][2] normalizes into world.ItemBonusSkills.
--
-- CHECK constraints below mirror legacy's load-time field validation (Item_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:889-1190), which rejects the whole 99999-slot Load_Item call on the first
-- offending record -- fatal to both ts25sharemem's and every ts25zone process's boot sequence. Every bound
-- was cross-checked against the real seeded data (34,421 rows, Migrations/Seed/world/080_items.sql) before
-- being added -- none of them reject any currently-seeded row. Constants used: MAX_ITEM_TYPE_NUM_CHK=8,
-- MAX_ITEM_SORT_NUM_CHK=99 (:880-881), MAX_ITEM_STATUS_ATTRIBUTE=10000, MAX_ITEM_STATUS_ATTRIBUTE1=13000,
-- MAX_ITEM_STATUS_ATTRIBUTE2=20000 (:883-885), MAX_NUMBER_SIZE=2000000000 (Server/Header/Protocol/DEFINE.h:365).
-- GainSkillNumber's own legacy bound (0-300, :1148) is deliberately not duplicated as a CHECK here -- the
-- existing FK_Items_Skills_GainSkillNumber is strictly tighter (world.Skills currently tops out at SkillId
-- 293, and any value must already exist as a real row there, not just fall in a numeric range). Level/
-- LevelLimit's own legacy bound (1-145 = MAX_LIMIT_LEVEL_NUM, :960,:995) is likewise already covered by
-- FK_Items_Levels_Level/LevelLimit (world.Levels is exactly rows 1-145) rather than restated as a CHECK.
CREATE TABLE world.Items
(
    ItemId               INT          NOT NULL,
    Name                 NVARCHAR(25) NOT NULL,
    Description1         NVARCHAR(51) NULL,
    Description2         NVARCHAR(51) NULL,
    Description3         NVARCHAR(51) NULL,
    Type                 TINYINT      NOT NULL,
    Sort                 TINYINT      NOT NULL,
    DataNumber2D         SMALLINT     NOT NULL,
    DataNumber3D         SMALLINT     NOT NULL, -- legacy valid range 0-10000 (Server/Header/S15_MyShare.cpp:950-959); SMALLINT to fit, see CK_Items_DataNumber3D
    AddDataNumber3D      SMALLINT     NOT NULL, -- always 0 in current data; reserved slot in ITEM_INFO, kept for fidelity; same 0-10000 legacy range/widening as DataNumber3D
    Level                SMALLINT     NOT NULL,
    MartialLevel         TINYINT      NOT NULL,
    EquipInfo1           TINYINT      NOT NULL,
    EquipInfo2           TINYINT      NOT NULL,
    BuyCost              INT          NOT NULL,
    SellCost             INT          NOT NULL,
    BuyCost2             INT          NOT NULL,
    LevelLimit           SMALLINT     NOT NULL,
    MartialLevelLimit    TINYINT      NOT NULL,
    CheckMonsterDrop     TINYINT      NOT NULL,
    CheckNpcSell         TINYINT      NOT NULL,
    CheckNpcShop         TINYINT      NOT NULL,
    CheckAvatarDrop      TINYINT      NOT NULL,
    CheckAvatarTrade     TINYINT      NOT NULL,
    CheckAvatarShop      TINYINT      NOT NULL,
    CheckImprove         TINYINT      NOT NULL,
    CheckHighImprove     TINYINT      NOT NULL,
    CheckHighItem        TINYINT      NOT NULL,
    CheckLowItem         TINYINT      NOT NULL,
    CheckExchange        TINYINT      NOT NULL,
    CheckSetItem         TINYINT      NOT NULL,
    CheckDateItem        SMALLINT     NOT NULL, -- despite the "Check" prefix, a day count; legacy valid range is 0-365 (Server/Header/S15_MyShare.cpp:1065-1069), not 0-30 -- SMALLINT to fit, see CK_Items_CheckDateItem
    Strength             SMALLINT     NOT NULL,
    Dexterity            SMALLINT     NOT NULL,
    Vitality             SMALLINT     NOT NULL,
    Intelligent          SMALLINT     NOT NULL,
    Luck                 SMALLINT     NOT NULL,
    AttackPower          SMALLINT     NOT NULL,
    DefensePower         SMALLINT     NOT NULL,
    AttackSuccess        SMALLINT     NOT NULL,
    AttackBlock          SMALLINT     NOT NULL,
    ElementAttackPower   SMALLINT     NOT NULL,
    ElementDefensePower  SMALLINT     NOT NULL,
    Critical             TINYINT      NOT NULL,
    PotionType1          SMALLINT     NOT NULL,
    PotionType2          SMALLINT     NOT NULL,
    GainSkillNumber      INT          NULL,
    LastAttackBonusInfo1 SMALLINT     NOT NULL,
    LastAttackBonusInfo2 SMALLINT     NOT NULL,
    CapeInfo1            TINYINT      NOT NULL,
    CapeInfo2            TINYINT      NOT NULL,
    CapeInfo3            TINYINT      NOT NULL,
    CONSTRAINT PK_Items PRIMARY KEY CLUSTERED (ItemId),
    CONSTRAINT FK_Items_Levels_Level FOREIGN KEY (Level) REFERENCES world.Levels (Level),
    CONSTRAINT FK_Items_Levels_LevelLimit FOREIGN KEY (LevelLimit) REFERENCES world.Levels (Level),
    CONSTRAINT FK_Items_Skills_GainSkillNumber FOREIGN KEY (GainSkillNumber) REFERENCES world.Skills (SkillId),
    -- CheckDateItem/DataNumber3D/AddDataNumber3D CHECKs mirror legacy's own load-time field validation
    -- (Item_CheckValidElement, Server/Header/S15_MyShare.cpp); once a column is widened past the legacy
    -- bound it exists to represent (TINYINT -> SMALLINT above), the column type alone no longer enforces
    -- that bound, so these constraints close the gap the widening itself opens.
    CONSTRAINT CK_Items_CheckDateItem CHECK (CheckDateItem BETWEEN 0 AND 365),
    CONSTRAINT CK_Items_DataNumber3D CHECK (DataNumber3D BETWEEN 0 AND 10000),
    CONSTRAINT CK_Items_AddDataNumber3D CHECK (AddDataNumber3D BETWEEN 0 AND 10000),
    CONSTRAINT CK_Items_Type CHECK (Type BETWEEN 1 AND 8),                    -- :935, MAX_ITEM_TYPE_NUM_CHK
    CONSTRAINT CK_Items_Sort CHECK (Sort BETWEEN 1 AND 99),                  -- :940, MAX_ITEM_SORT_NUM_CHK
    CONSTRAINT CK_Items_DataNumber2D CHECK (DataNumber2D BETWEEN 1 AND 10000), -- :945
    CONSTRAINT CK_Items_MartialLevel CHECK (MartialLevel BETWEEN 0 AND 25),  -- :965
    CONSTRAINT CK_Items_EquipInfo CHECK (                                    -- :970-979
        EquipInfo1 BETWEEN 1 AND 4 AND
        EquipInfo2 BETWEEN 1 AND 14
        ),
    CONSTRAINT CK_Items_Costs CHECK (                                       -- :980-994, MAX_NUMBER_SIZE
        BuyCost BETWEEN 1 AND 2000000000 AND
        SellCost BETWEEN 0 AND 2000000000 AND
        BuyCost2 BETWEEN 0 AND 2000000000
        ),
    CONSTRAINT CK_Items_MartialLevelLimit CHECK (MartialLevelLimit BETWEEN 0 AND 25), -- :1000
    -- The 11 "Check*" gameplay-permission flags are all encoded 1=Yes/2=No in legacy (never 0/1), validated
    -- back-to-back over :1005-1059 -- one compound CHECK rather than 11 near-identical single-column ones.
    CONSTRAINT CK_Items_CheckFlags CHECK (
        CheckMonsterDrop BETWEEN 1 AND 2 AND
        CheckNpcSell BETWEEN 1 AND 2 AND
        CheckNpcShop BETWEEN 1 AND 2 AND
        CheckAvatarDrop BETWEEN 1 AND 2 AND
        CheckAvatarTrade BETWEEN 1 AND 2 AND
        CheckAvatarShop BETWEEN 1 AND 2 AND
        CheckImprove BETWEEN 1 AND 2 AND
        CheckHighImprove BETWEEN 1 AND 2 AND
        CheckHighItem BETWEEN 1 AND 2 AND
        CheckLowItem BETWEEN 1 AND 2 AND
        CheckExchange BETWEEN 1 AND 2
        ),
    CONSTRAINT CK_Items_CheckSetItem CHECK (CheckSetItem BETWEEN 1 AND 3), -- :1060, distinct 1-3 bound
    -- Strength/Dexterity/Vitality/Intelligent/Luck/AttackBlock all share the same MAX_ITEM_STATUS_ATTRIBUTE
    -- (10000) ceiling and are validated back-to-back over :1070-1090,:1110.
    CONSTRAINT CK_Items_CoreAttributes CHECK (
        Strength BETWEEN 0 AND 10000 AND
        Dexterity BETWEEN 0 AND 10000 AND
        Vitality BETWEEN 0 AND 10000 AND
        Intelligent BETWEEN 0 AND 10000 AND
        Luck BETWEEN 0 AND 10000 AND
        AttackBlock BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_Items_AttackPower CHECK (AttackPower BETWEEN 0 AND 20000),     -- :1095, MAX_ITEM_STATUS_ATTRIBUTE2
    CONSTRAINT CK_Items_DefensePower CHECK (DefensePower BETWEEN 0 AND 13000),   -- :1100, MAX_ITEM_STATUS_ATTRIBUTE1
    CONSTRAINT CK_Items_AttackSuccess CHECK (AttackSuccess BETWEEN 0 AND 20000), -- :1105, MAX_ITEM_STATUS_ATTRIBUTE2
    CONSTRAINT CK_Items_ElementPowers CHECK (                                    -- :1115-1120, MAX_ITEM_STATUS_ATTRIBUTE1
        ElementAttackPower BETWEEN 0 AND 13000 AND
        ElementDefensePower BETWEEN 0 AND 13000
        ),
    CONSTRAINT CK_Items_Critical CHECK (Critical BETWEEN 0 AND 100), -- :1125
    -- PotionType1's own 0-16 bound (:1130) is now enforced for full parity with every other ordinary
    -- per-column legacy bound above (previously left out as an inconsistency; see git history for the prior
    -- "deliberately not enforced" wording this replaces). PotionType2's own bound is conditional on
    -- PotionType1, and stays the separate compound CK_Items_PotionType2Range below.
    CONSTRAINT CK_Items_PotionType1 CHECK (PotionType1 BETWEEN 0 AND 16),
    -- PotionType2's valid range is normally 0-10000, but narrows to 1-3 whenever PotionType1 equals 9
    -- (Server/Header/S15_MyShare.cpp:1130-1147). A plain single-column CHECK on PotionType2 cannot express
    -- a bound conditional on a sibling column's value, hence this compound (multi-column, same-row) CHECK.
    CONSTRAINT CK_Items_PotionType2Range CHECK (
        (PotionType1 = 9 AND PotionType2 BETWEEN 1 AND 3)
            OR (PotionType1 <> 9 AND PotionType2 BETWEEN 0 AND 10000)
        ),
    CONSTRAINT CK_Items_LastAttackBonusInfo CHECK (       -- :1153-1162
        LastAttackBonusInfo1 BETWEEN 0 AND 100 AND
        LastAttackBonusInfo2 BETWEEN 0 AND 1000
        ),
    CONSTRAINT CK_Items_CapeInfo CHECK (                  -- :1163-1177
        CapeInfo1 BETWEEN 0 AND 100 AND
        CapeInfo2 BETWEEN 0 AND 100 AND
        CapeInfo3 BETWEEN 0 AND 100
        )
);
