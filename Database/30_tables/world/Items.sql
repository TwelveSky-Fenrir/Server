-- Source: legacy ITEM_INFO (005_00002.IMG via ItemReader.ReadAll -- post-runtime-patch data, i.e. what
-- the live legacy server actually serves, not the raw on-disk bytes). One row per ItemRecord where
-- Index != 0: the runtime patch (retired-slot zeroing at item ids 89501-89562 and 99001, applied every
-- server boot by the legacy MyShm::Load_Item) is the legacy "deleted item" convention, so those ~65,600
-- slots carry no real data and are not seeded as empty placeholder rows.
-- Column order mirrors ITEM_INFO (Header/Protocol/STRUCT.h:44-93) field-for-field so this stays
-- diffable against the source struct. EquipInfo[2]/PotionType[2]/LastAttackBonusInfo[2]/CapeInfo[3] are
-- small, always-meaningful fixed pairs/triples (2-3 slots, never sparse) so they get numbered columns;
-- BonusSkillInfo[8][2] is normalized out to world.ItemBonusSkills (see that file) since most of its 8
-- slots are empty per item (only 15,417 of 34,353 real items use any slot at all).
-- GainSkillNumber keeps its legacy name (iGainSkillNumber) but is modeled as a nullable FK to
-- world.Skills: every one of its 78 distinct non-zero values in the real data is a valid SkillId, so
-- despite the "Number" in its name this is a skill reference, not a quantity. The legacy "0 = no skill"
-- sentinel translates to NULL, since SkillId 0 is not a real row in world.Skills.
-- Level/LevelLimit are modeled as FKs into world.Levels(Level): both fields' observed range (1-145,
-- never 0) exactly matches world.Levels' full, gapless 1-145 span, so the FK can never fail on real
-- data and catches typos in any future hand-authored item row.
CREATE TABLE world.Items
(
    ItemId               INT      NOT NULL,
    Name                 NVARCHAR(25) NOT NULL,
    Description1         NVARCHAR(51) NULL,
    Description2         NVARCHAR(51) NULL,
    Description3         NVARCHAR(51) NULL,
    Type                 TINYINT  NOT NULL,
    Sort                 TINYINT  NOT NULL,
    DataNumber2D         SMALLINT NOT NULL,
    DataNumber3D         TINYINT  NOT NULL,
    AddDataNumber3D      TINYINT  NOT NULL, -- always 0 in current data; reserved slot in ITEM_INFO, kept for fidelity
    Level                SMALLINT NOT NULL,
    MartialLevel         TINYINT  NOT NULL,
    EquipInfo1           TINYINT  NOT NULL,
    EquipInfo2           TINYINT  NOT NULL,
    BuyCost              INT      NOT NULL,
    SellCost             INT      NOT NULL,
    BuyCost2             INT      NOT NULL,
    LevelLimit           SMALLINT NOT NULL,
    MartialLevelLimit    TINYINT  NOT NULL,
    CheckMonsterDrop     TINYINT  NOT NULL,
    CheckNpcSell         TINYINT  NOT NULL,
    CheckNpcShop         TINYINT  NOT NULL,
    CheckAvatarDrop      TINYINT  NOT NULL,
    CheckAvatarTrade     TINYINT  NOT NULL,
    CheckAvatarShop      TINYINT  NOT NULL,
    CheckImprove         TINYINT  NOT NULL,
    CheckHighImprove     TINYINT  NOT NULL,
    CheckHighItem        TINYINT  NOT NULL,
    CheckLowItem         TINYINT  NOT NULL,
    CheckExchange        TINYINT  NOT NULL,
    CheckSetItem         TINYINT  NOT NULL,
    CheckDateItem        TINYINT  NOT NULL, -- despite the "Check" prefix this ranges 0-30, not boolean (likely a day count)
    Strength             SMALLINT NOT NULL,
    Dexterity            SMALLINT NOT NULL,
    Vitality             SMALLINT NOT NULL,
    Intelligent          SMALLINT NOT NULL,
    Luck                 SMALLINT NOT NULL,
    AttackPower          SMALLINT NOT NULL,
    DefensePower         SMALLINT NOT NULL,
    AttackSuccess        SMALLINT NOT NULL,
    AttackBlock          SMALLINT NOT NULL,
    ElementAttackPower   SMALLINT NOT NULL,
    ElementDefensePower  SMALLINT NOT NULL,
    Critical             TINYINT  NOT NULL,
    PotionType1          SMALLINT NOT NULL,
    PotionType2          SMALLINT NOT NULL,
    GainSkillNumber      INT NULL,
    LastAttackBonusInfo1 SMALLINT NOT NULL,
    LastAttackBonusInfo2 SMALLINT NOT NULL,
    CapeInfo1            TINYINT  NOT NULL,
    CapeInfo2            TINYINT  NOT NULL,
    CapeInfo3            TINYINT  NOT NULL,
    CONSTRAINT PK_Items PRIMARY KEY CLUSTERED (ItemId),
    CONSTRAINT FK_Items_Levels_Level FOREIGN KEY (Level) REFERENCES world.Levels (Level),
    CONSTRAINT FK_Items_Levels_LevelLimit FOREIGN KEY (LevelLimit) REFERENCES world.Levels (Level),
    CONSTRAINT FK_Items_Skills_GainSkillNumber FOREIGN KEY (GainSkillNumber) REFERENCES world.Skills (SkillId)
);
