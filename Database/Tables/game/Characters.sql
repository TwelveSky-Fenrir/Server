CREATE TABLE game.Characters
(
    CharacterId              INT IDENTITY (1,1)                                NOT NULL,
    AccountId                INT                                               NOT NULL,
    Slot                     TINYINT                                           NOT NULL,
    Name                     NVARCHAR(13) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,          
    Tribe                    TINYINT                                           NOT NULL
        CONSTRAINT CK_Characters_Tribe CHECK (Tribe BETWEEN 0 AND 3),                             
    Gender                   TINYINT                                           NOT NULL,
    HeadType                 TINYINT                                           NOT NULL,
    FaceType                 TINYINT                                           NOT NULL,
    Level                    SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_Level DEFAULT 1,
    Level2                   SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_Level2 DEFAULT 1,                                                
    Exp2                     INT                                               NOT NULL
        CONSTRAINT DF_Characters_Exp2 DEFAULT 0
        CONSTRAINT CK_Characters_Exp2 CHECK (Exp2 >= 0),                                          
    MapId                    SMALLINT                                          NOT NULL,
    PosX                     REAL                                              NOT NULL,
    PosY                     REAL                                              NOT NULL,
    PosZ                     REAL                                              NOT NULL,
    Heading                  REAL                                              NOT NULL
        CONSTRAINT DF_Characters_Heading DEFAULT 0,                                               
    Life                     INT                                               NOT NULL,
    MaxLife                  INT                                               NOT NULL,
    Mana                     INT                                               NOT NULL,
    MaxMana                  INT                                               NOT NULL,
    Experience               BIGINT                                            NOT NULL
        CONSTRAINT DF_Characters_Experience DEFAULT 0
        CONSTRAINT CK_Characters_Experience CHECK (Experience >= 0),                              
    StatVit                  INT                                               NOT NULL
        CONSTRAINT DF_Characters_StatVit DEFAULT 0,
    StatStr                  INT                                               NOT NULL
        CONSTRAINT DF_Characters_StatStr DEFAULT 0,
    StatInt                  INT                                               NOT NULL
        CONSTRAINT DF_Characters_StatInt DEFAULT 0,
    StatDex                  INT                                               NOT NULL
        CONSTRAINT DF_Characters_StatDex DEFAULT 0,
    StatPoints               INT                                               NOT NULL
        CONSTRAINT DF_Characters_StatPoints DEFAULT 0,
    SkillPoints              INT                                               NOT NULL
        CONSTRAINT DF_Characters_SkillPoints DEFAULT 0,
    Money                    BIGINT                                            NOT NULL
        CONSTRAINT DF_Characters_Money DEFAULT 0
        CONSTRAINT CK_Characters_Money CHECK (Money >= 0),
    BigMoney                 INT                                               NOT NULL
        CONSTRAINT DF_Characters_BigMoney DEFAULT 0
        CONSTRAINT CK_Characters_BigMoney CHECK (BigMoney >= 0),
    StoreMoney               BIGINT                                            NOT NULL
        CONSTRAINT DF_Characters_StoreMoney DEFAULT 0
        CONSTRAINT CK_Characters_StoreMoney CHECK (StoreMoney >= 0),
    BigStoreMoney            INT                                               NOT NULL
        CONSTRAINT DF_Characters_BigStoreMoney DEFAULT 0
        CONSTRAINT CK_Characters_BigStoreMoney CHECK (BigStoreMoney >= 0),
    RebirthCount             TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_RebirthCount DEFAULT 0
        CONSTRAINT CK_Characters_RebirthCount CHECK (RebirthCount BETWEEN 0 AND 12),              
    Title                    INT                                               NOT NULL
        CONSTRAINT DF_Characters_Title DEFAULT 0,
    Halo                     INT                                               NOT NULL
        CONSTRAINT DF_Characters_Halo DEFAULT 0,
    ContributionPoints       INT                                               NOT NULL
        CONSTRAINT DF_Characters_ContributionPoints DEFAULT 0,                                    
    TeacherPoint             INT                                               NOT NULL
        CONSTRAINT DF_Characters_TeacherPoint DEFAULT 0
        CONSTRAINT CK_Characters_TeacherPoint CHECK (TeacherPoint >= 0),                          
    EatLifePotion            SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_EatLifePotion DEFAULT 0
        CONSTRAINT CK_Characters_EatLifePotion CHECK (EatLifePotion BETWEEN 0 AND 400),
    EatManaPotion            SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_EatManaPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatManaPotion CHECK (EatManaPotion BETWEEN 0 AND 400),
    EatStrPotion             SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_EatStrPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatStrPotion CHECK (EatStrPotion BETWEEN 0 AND 400),
    EatDexPotion             SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_EatDexPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatDexPotion CHECK (EatDexPotion BETWEEN 0 AND 400),
    EatElePotion             SMALLINT                                          NOT NULL
        CONSTRAINT DF_Characters_EatElePotion DEFAULT 0
        CONSTRAINT CK_Characters_EatElePotion CHECK (EatElePotion BETWEEN 0 AND 400),
    ProtectForDeath          INT                                               NOT NULL
        CONSTRAINT DF_Characters_ProtectForDeath DEFAULT 0
        CONSTRAINT CK_Characters_ProtectForDeath CHECK (ProtectForDeath >= 0),
    ProtectForDestroy        INT                                               NOT NULL
        CONSTRAINT DF_Characters_ProtectForDestroy DEFAULT 0
        CONSTRAINT CK_Characters_ProtectForDestroy CHECK (ProtectForDestroy >= 0),
    DoubleExpTime1           INT                                               NOT NULL
        CONSTRAINT DF_Characters_DoubleExpTime1 DEFAULT 0,                                        
    DoubleExpTime2           INT                                               NOT NULL
        CONSTRAINT DF_Characters_DoubleExpTime2 DEFAULT 0,                                        
    AutoBuffTime             INT                                               NOT NULL
        CONSTRAINT DF_Characters_AutoBuffTime DEFAULT 0,                                          
    DropItemTime             INT                                               NOT NULL
        CONSTRAINT DF_Characters_DropItemTime DEFAULT 0,
    InventoryDate            INT                                               NOT NULL
        CONSTRAINT DF_Characters_InventoryDate DEFAULT 0,                                         
    StoreDate                INT                                               NOT NULL
        CONSTRAINT DF_Characters_StoreDate DEFAULT 0,
    BloodCoin                INT                                               NOT NULL
        CONSTRAINT DF_Characters_BloodCoin DEFAULT 0
        CONSTRAINT CK_Characters_BloodCoin CHECK (BloodCoin >= 0),
    RewardClaimDay           TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_RewardClaimDay DEFAULT 0
        CONSTRAINT CK_Characters_RewardClaimDay CHECK (RewardClaimDay BETWEEN 0 AND 7),
    RewardClaimDate          INT                                               NOT NULL
        CONSTRAINT DF_Characters_RewardClaimDate DEFAULT 0,
    TeacherCharacterId       INT                                               NULL,
    StudentCharacterId       INT                                               NULL,
    JoinWar                  INT                                               NOT NULL
        CONSTRAINT DF_Characters_JoinWar DEFAULT 0
        CONSTRAINT CK_Characters_JoinWar CHECK (JoinWar >= 0),
    MissionKillOtherTribe    INT                                               NOT NULL
        CONSTRAINT DF_Characters_MissionKillOtherTribe DEFAULT 0
        CONSTRAINT CK_Characters_MissionKillOtherTribe CHECK (MissionKillOtherTribe >= 0),        
    MissionKillMonster       INT                                               NOT NULL
        CONSTRAINT DF_Characters_MissionKillMonster DEFAULT 0
        CONSTRAINT CK_Characters_MissionKillMonster CHECK (MissionKillMonster >= 0),
    MissionPlayTime          INT                                               NOT NULL
        CONSTRAINT DF_Characters_MissionPlayTime DEFAULT 0
        CONSTRAINT CK_Characters_MissionPlayTime CHECK (MissionPlayTime >= 0),
    AutoHuntEnabled          BIT                                               NOT NULL
        CONSTRAINT DF_Characters_AutoHuntEnabled DEFAULT 0,
    AutoHuntConfig           VARBINARY(112)                                    NOT NULL
        CONSTRAINT DF_Characters_AutoHuntConfig DEFAULT 0x,
    AutoLifeRatio            TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_AutoLifeRatio DEFAULT 0
        CONSTRAINT CK_Characters_AutoLifeRatio CHECK (AutoLifeRatio BETWEEN 0 AND 5),
    AutoManaRatio            TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_AutoManaRatio DEFAULT 0
        CONSTRAINT CK_Characters_AutoManaRatio CHECK (AutoManaRatio BETWEEN 0 AND 5),
    PetGrowth                INT                                               NOT NULL
        CONSTRAINT DF_Characters_PetGrowth DEFAULT 0
        CONSTRAINT CK_Characters_PetGrowth CHECK (PetGrowth >= 0),
    PetActivity              TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_PetActivity DEFAULT 0
        CONSTRAINT CK_Characters_PetActivity CHECK (PetActivity BETWEEN 0 AND 100),
    TribeTransferPermitCount INT                                               NOT NULL
        CONSTRAINT DF_Characters_TribeTransferPermitCount DEFAULT 0
        CONSTRAINT CK_Characters_TribeTransferPermitCount CHECK (TribeTransferPermitCount >= 0),  
    PremiumExpireUtc         BIGINT                                            NOT NULL
        CONSTRAINT DF_Characters_PremiumExpireUtc DEFAULT 0,                                      
    PreviousTribe            TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_PreviousTribe DEFAULT 0
        CONSTRAINT CK_Characters_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),             
    MountItemId              INT                                               NOT NULL
        CONSTRAINT DF_Characters_MountItemId DEFAULT 0,                                           
    MountExpActivity         INT                                               NOT NULL
        CONSTRAINT DF_Characters_MountExpActivity DEFAULT 0,                                      
    MountPower               INT                                               NOT NULL
        CONSTRAINT DF_Characters_MountPower DEFAULT 0,                                            
    MountSlotIndex           INT                                               NOT NULL
        CONSTRAINT DF_Characters_MountSlotIndex DEFAULT -1,                                       
    MountTime                INT                                               NOT NULL
        CONSTRAINT DF_Characters_MountTime DEFAULT 0,                                             
    AutoTime2                INT                                               NOT NULL
        CONSTRAINT DF_Characters_AutoTime2 DEFAULT 0
        CONSTRAINT CK_Characters_AutoTime2 CHECK (AutoTime2 >= 0),                                
    Zone241Time              INT                                               NOT NULL
        CONSTRAINT DF_Characters_Zone241Time DEFAULT 0
        CONSTRAINT CK_Characters_Zone241Time CHECK (Zone241Time >= 0),                            
    WarPoint                 INT                                               NOT NULL
        CONSTRAINT DF_Characters_WarPoint DEFAULT 0
        CONSTRAINT CK_Characters_WarPoint CHECK (WarPoint >= 0),                                  
    PetBagDate               INT                                               NOT NULL
        CONSTRAINT DF_Characters_PetBagDate DEFAULT 0,                                            
    M15PetLuckyBoxPity       TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_M15PetLuckyBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_M15PetLuckyBoxPity CHECK (M15PetLuckyBoxPity BETWEEN 0 AND 200), 
    VisibleState             TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_VisibleState DEFAULT 1,                                          
    SpecialState             TINYINT                                           NOT NULL
        CONSTRAINT DF_Characters_SpecialState DEFAULT 0,
    CostumeIndex             INT                                               NOT NULL
        CONSTRAINT DF_Characters_CostumeIndex DEFAULT -1
        CONSTRAINT CK_Characters_CostumeIndex CHECK (CostumeIndex BETWEEN -1 AND 9),              
    FlushSequence            BIGINT                                            NOT NULL
        CONSTRAINT DF_Characters_FlushSequence DEFAULT 0,                                         
    CreatedAtUtc             DATETIME2(3)                                      NOT NULL
        CONSTRAINT DF_Characters_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc             DATETIME2(3)                                      NOT NULL
        CONSTRAINT DF_Characters_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Characters PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT UQ_Characters_Name UNIQUE (Name),
    CONSTRAINT UQ_Characters_Account_Slot UNIQUE (AccountId, Slot),
    CONSTRAINT CK_Characters_Slot CHECK (Slot BETWEEN 0 AND 2),
    CONSTRAINT FK_Characters_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Characters_TeacherCharacter FOREIGN KEY (TeacherCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_Characters_StudentCharacter FOREIGN KEY (StudentCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT CK_Characters_TeacherNotSelf CHECK (TeacherCharacterId IS NULL OR TeacherCharacterId <> CharacterId),
    CONSTRAINT CK_Characters_StudentNotSelf CHECK (StudentCharacterId IS NULL OR StudentCharacterId <> CharacterId),
    INDEX IX_Characters_Account NONCLUSTERED (AccountId) INCLUDE (Slot, Name, Tribe, Level),
    INDEX IX_Characters_MaxLevel_TribePoint NONCLUSTERED (Tribe, Level) INCLUDE (Level2, RebirthCount) WHERE (Level >= 145)
);
