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
    DataNumber3D         SMALLINT     NOT NULL,                                       
    AddDataNumber3D      SMALLINT     NOT NULL,                                       
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
    CheckDateItem        SMALLINT     NOT NULL,                                       
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
    CONSTRAINT CK_Items_CheckDateItem CHECK (CheckDateItem BETWEEN 0 AND 365),
    CONSTRAINT CK_Items_DataNumber3D CHECK (DataNumber3D BETWEEN 0 AND 10000),
    CONSTRAINT CK_Items_AddDataNumber3D CHECK (AddDataNumber3D BETWEEN 0 AND 10000),
    CONSTRAINT CK_Items_Type CHECK (Type BETWEEN 1 AND 8),                            
    CONSTRAINT CK_Items_Sort CHECK (Sort BETWEEN 1 AND 99),                           
    CONSTRAINT CK_Items_DataNumber2D CHECK (DataNumber2D BETWEEN 1 AND 10000),        
    CONSTRAINT CK_Items_MartialLevel CHECK (MartialLevel BETWEEN 0 AND 25),           
    CONSTRAINT CK_Items_EquipInfo CHECK (                                             
        EquipInfo1 BETWEEN 1 AND 4 AND
        EquipInfo2 BETWEEN 1 AND 14
        ),
    CONSTRAINT CK_Items_Costs CHECK (                                                 
        BuyCost BETWEEN 1 AND 2000000000 AND
        SellCost BETWEEN 0 AND 2000000000 AND
        BuyCost2 BETWEEN 0 AND 2000000000
        ),
    CONSTRAINT CK_Items_MartialLevelLimit CHECK (MartialLevelLimit BETWEEN 0 AND 25), 
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
    CONSTRAINT CK_Items_CheckSetItem CHECK (CheckSetItem BETWEEN 1 AND 3),            
    CONSTRAINT CK_Items_CoreAttributes CHECK (
        Strength BETWEEN 0 AND 10000 AND
        Dexterity BETWEEN 0 AND 10000 AND
        Vitality BETWEEN 0 AND 10000 AND
        Intelligent BETWEEN 0 AND 10000 AND
        Luck BETWEEN 0 AND 10000 AND
        AttackBlock BETWEEN 0 AND 10000
        ),
    CONSTRAINT CK_Items_AttackPower CHECK (AttackPower BETWEEN 0 AND 20000),          
    CONSTRAINT CK_Items_DefensePower CHECK (DefensePower BETWEEN 0 AND 13000),        
    CONSTRAINT CK_Items_AttackSuccess CHECK (AttackSuccess BETWEEN 0 AND 20000),      
    CONSTRAINT CK_Items_ElementPowers CHECK (                                         
        ElementAttackPower BETWEEN 0 AND 13000 AND
        ElementDefensePower BETWEEN 0 AND 13000
        ),
    CONSTRAINT CK_Items_Critical CHECK (Critical BETWEEN 0 AND 100),                  
    CONSTRAINT CK_Items_PotionType1 CHECK (PotionType1 BETWEEN 0 AND 16),
    CONSTRAINT CK_Items_PotionType2Range CHECK (
        (PotionType1 = 9 AND PotionType2 BETWEEN 1 AND 3)
            OR (PotionType1 <> 9 AND PotionType2 BETWEEN 0 AND 10000)
        ),
    CONSTRAINT CK_Items_LastAttackBonusInfo CHECK (                                   
        LastAttackBonusInfo1 BETWEEN 0 AND 100 AND
        LastAttackBonusInfo2 BETWEEN 0 AND 1000
        ),
    CONSTRAINT CK_Items_CapeInfo CHECK (                                              
        CapeInfo1 BETWEEN 0 AND 100 AND
        CapeInfo2 BETWEEN 0 AND 100 AND
        CapeInfo3 BETWEEN 0 AND 100
        )
);
