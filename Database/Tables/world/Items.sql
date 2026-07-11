-- Legacy ITEM_INFO (post-runtime-patch data, i.e. what the live legacy server actually serves); one row per record where Index != 0.
-- GainSkillNumber is a nullable FK to world.Skills despite the "Number" name -- it's a skill reference, not a quantity (0 -> NULL). BonusSkillInfo[8][2] normalizes into world.ItemBonusSkills.
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
    -- PotionType2's valid range is normally 0-10000, but narrows to 1-3 whenever PotionType1 equals 9
    -- (Server/Header/S15_MyShare.cpp:1130-1147). A plain single-column CHECK on PotionType2 cannot express
    -- a bound conditional on a sibling column's value, hence this compound (multi-column, same-row) CHECK.
    -- PotionType1's own 0-16 bound is deliberately not enforced here -- it is an ordinary per-column legacy
    -- bound like Type 1-8 or Sort 1-99, none of which this schema enforces at the storage level; those stay
    -- boot-time-loader validation.
    CONSTRAINT CK_Items_PotionType2Range CHECK (
        (PotionType1 = 9 AND PotionType2 BETWEEN 1 AND 3)
            OR (PotionType1 <> 9 AND PotionType2 BETWEEN 0 AND 10000)
        )
);
