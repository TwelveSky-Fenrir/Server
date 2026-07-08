-- Legacy: wAvatar/AVATAR_INFO. Money/BigMoney and StoreMoney/BigStoreMoney mirror the legacy sub-billion
-- int + billions-overflow-counter split (aMoney/aBigMoney) -- kept as BIGINT here for headroom, but the
-- legacy per-pool caps still apply app-side.
-- BloodCoin: no earn path exists in the available legacy source, only spend/decrement -- starts at 0.
-- RewardClaimDay/RewardClaimDate: Fenrir computes the legacy's weekly reset lazily from RewardClaimDate
-- instead of a background job -- see usp_Character_GetRewardClaimState/ClaimDailyReward.
-- PetGrowth/PetActivity: one counter per character (legacy tracks growth per equipped pet item instance),
-- reset on pet-item swap. TeacherPoint is unrelated to the TeacherCharacterId/StudentCharacterId mentor bond.
-- TribeTransferPermitCount: no legacy source available here documents the actual tribe-change mechanic a
-- Faction Transfer Scroll (world.Items 8153/8154) unlocks -- only the permit-banking half is implemented
-- (usp_Character_GrantTribeTransferPermit), the mirror image of BloodCoin's "spend but no grant" gap above.
CREATE TABLE game.Characters
(
    CharacterId              INT IDENTITY (1,1) NOT NULL,
    AccountId                INT                NOT NULL,
    Slot                     TINYINT            NOT NULL,
    Name                     NVARCHAR(13)       NOT NULL,                                        -- MAX_AVATAR_NAME_LENGTH=13, a real wire truncation limit
    Tribe                    TINYINT            NOT NULL,
    Gender                   TINYINT            NOT NULL,
    HeadType                 TINYINT            NOT NULL,
    FaceType                 TINYINT            NOT NULL,
    Level                    SMALLINT           NOT NULL
        CONSTRAINT DF_Characters_Level DEFAULT 1,
    Level2                   SMALLINT           NOT NULL
        CONSTRAINT DF_Characters_Level2 DEFAULT 1,                                               -- aLevel2, the post-Level-145 "high level" rebirth ladder (1-12, MAX_LIMIT_HIGH_LEVEL_NUM)
    Exp2                     INT                NOT NULL
        CONSTRAINT DF_Characters_Exp2 DEFAULT 0
        CONSTRAINT CK_Characters_Exp2 CHECK (Exp2 >= 0),                                         -- aExp2, Level2's own XP counter; reset to 0 on every successful Max Rebirth
    MapId                    SMALLINT           NOT NULL,
    PosX                     REAL               NOT NULL,
    PosY                     REAL               NOT NULL,
    PosZ                     REAL               NOT NULL,
    Heading                  REAL               NOT NULL
        CONSTRAINT DF_Characters_Heading DEFAULT 0,                                              -- ACTION_INFO.aFront
    Life                     INT                NOT NULL,
    MaxLife                  INT                NOT NULL,
    Mana                     INT                NOT NULL,
    MaxMana                  INT                NOT NULL,
    Experience               BIGINT             NOT NULL
        CONSTRAINT DF_Characters_Experience DEFAULT 0
        CONSTRAINT CK_Characters_Experience CHECK (Experience >= 0),                             -- aExp1 (NOT "aExp1+aExp2 combined" -- Server/BuildEU33/DB/nxtserver.sql:41-43 and Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:229-240 both persist aExp1/aExp2 as two fully independent values, never summed; see Exp2 below)
    StatVit                  INT                NOT NULL
        CONSTRAINT DF_Characters_StatVit DEFAULT 0,
    StatStr                  INT                NOT NULL
        CONSTRAINT DF_Characters_StatStr DEFAULT 0,
    StatInt                  INT                NOT NULL
        CONSTRAINT DF_Characters_StatInt DEFAULT 0,
    StatDex                  INT                NOT NULL
        CONSTRAINT DF_Characters_StatDex DEFAULT 0,
    StatPoints               INT                NOT NULL
        CONSTRAINT DF_Characters_StatPoints DEFAULT 0,
    SkillPoints              INT                NOT NULL
        CONSTRAINT DF_Characters_SkillPoints DEFAULT 0,
    Money                    BIGINT             NOT NULL
        CONSTRAINT DF_Characters_Money DEFAULT 0
        CONSTRAINT CK_Characters_Money CHECK (Money >= 0),
    BigMoney                 INT                NOT NULL
        CONSTRAINT DF_Characters_BigMoney DEFAULT 0
        CONSTRAINT CK_Characters_BigMoney CHECK (BigMoney >= 0),
    StoreMoney               BIGINT             NOT NULL
        CONSTRAINT DF_Characters_StoreMoney DEFAULT 0
        CONSTRAINT CK_Characters_StoreMoney CHECK (StoreMoney >= 0),
    BigStoreMoney            INT                NOT NULL
        CONSTRAINT DF_Characters_BigStoreMoney DEFAULT 0
        CONSTRAINT CK_Characters_BigStoreMoney CHECK (BigStoreMoney >= 0),
    RebirthCount             INT                NOT NULL
        CONSTRAINT DF_Characters_RebirthCount DEFAULT 0
        CONSTRAINT CK_Characters_RebirthCount CHECK (RebirthCount BETWEEN 0 AND 12),             -- aRebirthNum; MAX_REBIRTH_LIMIT=12 (G1-G12) IS live in ReleaseEU33 via the Rebirth-Pill item path (WUSE_ITEM_632 unconditionally #define'd) -- see Application/Fenrir.Application.Game.Domain/Progression/RebirthProgression.cs. Path B (TribeActionService.RebirthAsync, CZ_TRIBE_WORK_SEND tSort 11) is separately app-capped at generation 6; only Path A (UseInventoryItemService's Rebirth-Pill branch) reaches 7-12. CHECK bound is defense-in-depth: both trigger paths already app-cap before writing (RebirthProgression.MaxRebirthGeneration), but this closes the gap for a future writer that doesn't re-validate.
    Title                    INT                NOT NULL
        CONSTRAINT DF_Characters_Title DEFAULT 0,
    Halo                     INT                NOT NULL
        CONSTRAINT DF_Characters_Halo DEFAULT 0,
    ContributionPoints       INT                NOT NULL
        CONSTRAINT DF_Characters_ContributionPoints DEFAULT 0,                                   -- aKillOtherTribe (CP)
    TeacherPoint             INT                NOT NULL
        CONSTRAINT DF_Characters_TeacherPoint DEFAULT 0
        CONSTRAINT CK_Characters_TeacherPoint CHECK (TeacherPoint >= 0),                         -- quest reward type 5
    EatLifePotion            INT                NOT NULL
        CONSTRAINT DF_Characters_EatLifePotion DEFAULT 0
        CONSTRAINT CK_Characters_EatLifePotion CHECK (EatLifePotion BETWEEN 0 AND 400),
    EatManaPotion            INT                NOT NULL
        CONSTRAINT DF_Characters_EatManaPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatManaPotion CHECK (EatManaPotion BETWEEN 0 AND 400),
    EatStrPotion             INT                NOT NULL
        CONSTRAINT DF_Characters_EatStrPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatStrPotion CHECK (EatStrPotion BETWEEN 0 AND 400),
    EatDexPotion             INT                NOT NULL
        CONSTRAINT DF_Characters_EatDexPotion DEFAULT 0
        CONSTRAINT CK_Characters_EatDexPotion CHECK (EatDexPotion BETWEEN 0 AND 400),
    EatElePotion             INT                NOT NULL
        CONSTRAINT DF_Characters_EatElePotion DEFAULT 0
        CONSTRAINT CK_Characters_EatElePotion CHECK (EatElePotion BETWEEN 0 AND 400),
    ProtectForDeath          INT                NOT NULL
        CONSTRAINT DF_Characters_ProtectForDeath DEFAULT 0
        CONSTRAINT CK_Characters_ProtectForDeath CHECK (ProtectForDeath >= 0),
    ProtectForDestroy        INT                NOT NULL
        CONSTRAINT DF_Characters_ProtectForDestroy DEFAULT 0
        CONSTRAINT CK_Characters_ProtectForDestroy CHECK (ProtectForDestroy >= 0),
    DoubleExpTime1           INT                NOT NULL
        CONSTRAINT DF_Characters_DoubleExpTime1 DEFAULT 0,                                       -- aDoubleExpTime1: raw per-tick decrementing counter (NOT a YYYYMMDD date), 0 = no boost; welcome grant is the bare literal 300 (Server/ts25login/S04_MyWork02.cpp:886-887), decremented once per firing (Server/ts25zone/S07_MyGame04.cpp:955-969)
    DoubleExpTime2           INT                NOT NULL
        CONSTRAINT DF_Characters_DoubleExpTime2 DEFAULT 0,                                       -- aDoubleExpTime2, same decrementing-counter shape as DoubleExpTime1
    AutoBuffTime             INT                NOT NULL
        CONSTRAINT DF_Characters_AutoBuffTime DEFAULT 0,                                         -- aAutoBuffTime/aContinueSkillDay, YYYYMMDD (PlayerRuntimeState.AutoBuffTime encoding), 0 = no boost
    DropItemTime             INT                NOT NULL
        CONSTRAINT DF_Characters_DropItemTime DEFAULT 0,
    InventoryDate            INT                NOT NULL
        CONSTRAINT DF_Characters_InventoryDate DEFAULT 0,                                        -- bonus inventory page rental
    StoreDate                INT                NOT NULL
        CONSTRAINT DF_Characters_StoreDate DEFAULT 0,
    BloodCoin                INT                NOT NULL
        CONSTRAINT DF_Characters_BloodCoin DEFAULT 0
        CONSTRAINT CK_Characters_BloodCoin CHECK (BloodCoin >= 0),
    RewardClaimDay           TINYINT            NOT NULL
        CONSTRAINT DF_Characters_RewardClaimDay DEFAULT 0
        CONSTRAINT CK_Characters_RewardClaimDay CHECK (RewardClaimDay BETWEEN 0 AND 7),
    RewardClaimDate          INT                NOT NULL
        CONSTRAINT DF_Characters_RewardClaimDate DEFAULT 0,
    TeacherCharacterId       INT                NULL,
    StudentCharacterId       INT                NULL,
    JoinWar                  INT                NOT NULL
        CONSTRAINT DF_Characters_JoinWar DEFAULT 0
        CONSTRAINT CK_Characters_JoinWar CHECK (JoinWar >= 0),
    MissionKillOtherTribe    INT                NOT NULL
        CONSTRAINT DF_Characters_MissionKillOtherTribe DEFAULT 0
        CONSTRAINT CK_Characters_MissionKillOtherTribe CHECK (MissionKillOtherTribe >= 0),       -- aMissionDate.aKillOtherTribe, distinct from ContributionPoints
    MissionKillMonster       INT                NOT NULL
        CONSTRAINT DF_Characters_MissionKillMonster DEFAULT 0
        CONSTRAINT CK_Characters_MissionKillMonster CHECK (MissionKillMonster >= 0),
    MissionPlayTime          INT                NOT NULL
        CONSTRAINT DF_Characters_MissionPlayTime DEFAULT 0
        CONSTRAINT CK_Characters_MissionPlayTime CHECK (MissionPlayTime >= 0),
    AutoHuntEnabled          BIT                NOT NULL
        CONSTRAINT DF_Characters_AutoHuntEnabled DEFAULT 0,
    AutoHuntConfig           VARBINARY(112)     NOT NULL
        CONSTRAINT DF_Characters_AutoHuntConfig DEFAULT 0x,
    AutoLifeRatio            TINYINT            NOT NULL
        CONSTRAINT DF_Characters_AutoLifeRatio DEFAULT 0
        CONSTRAINT CK_Characters_AutoLifeRatio CHECK (AutoLifeRatio BETWEEN 0 AND 5),
    AutoManaRatio            TINYINT            NOT NULL
        CONSTRAINT DF_Characters_AutoManaRatio DEFAULT 0
        CONSTRAINT CK_Characters_AutoManaRatio CHECK (AutoManaRatio BETWEEN 0 AND 5),
    PetGrowth                INT                NOT NULL
        CONSTRAINT DF_Characters_PetGrowth DEFAULT 0
        CONSTRAINT CK_Characters_PetGrowth CHECK (PetGrowth >= 0),
    PetActivity              TINYINT            NOT NULL
        CONSTRAINT DF_Characters_PetActivity DEFAULT 0
        CONSTRAINT CK_Characters_PetActivity CHECK (PetActivity BETWEEN 0 AND 100),
    TribeTransferPermitCount INT                NOT NULL
        CONSTRAINT DF_Characters_TribeTransferPermitCount DEFAULT 0
        CONSTRAINT CK_Characters_TribeTransferPermitCount CHECK (TribeTransferPermitCount >= 0), -- banked Faction Transfer Scroll (world.Items 8153/8154) uses; no spend path exists yet, same posture as BloodCoin's absent grant path
    PremiumExpireUtc         BIGINT             NOT NULL
        CONSTRAINT DF_Characters_PremiumExpireUtc DEFAULT 0,                                     -- aPremium, Unix epoch seconds (0 = none); distinct scale from the YYYYMMDD ints above
    PreviousTribe            TINYINT            NOT NULL
        CONSTRAINT DF_Characters_PreviousTribe DEFAULT 0
        CONSTRAINT CK_Characters_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),            -- the Noble Dragon/Royal Serpent/Grand Tiger starter-kit template (0-2, never 3) that selected this character's equipment/skills/hotkeys -- genuinely independent from Tribe (0-3), checked for self-consistency at every zone entry (Server/ts25zone/S04_MyWork02.cpp:880-901)
    MountItemId              INT                NOT NULL
        CONSTRAINT DF_Characters_MountItemId DEFAULT 0,                                          -- 0 = no mount; ANIMAL_NUM_TIGER1 (1301) granted at creation
    MountExpActivity         INT                NOT NULL
        CONSTRAINT DF_Characters_MountExpActivity DEFAULT 0,                                     -- aAnimalExpActivity[0]
    MountPower               INT                NOT NULL
        CONSTRAINT DF_Characters_MountPower DEFAULT 0,                                           -- aAnimalPower[0]
    MountSlotIndex           INT                NOT NULL
        CONSTRAINT DF_Characters_MountSlotIndex DEFAULT -1,                                      -- aAnimalIndex; -1 = none active
    MountTime                INT                NOT NULL
        CONSTRAINT DF_Characters_MountTime DEFAULT 0,                                            -- aAnimalTime
    AutoTime2                INT                NOT NULL
        CONSTRAINT DF_Characters_AutoTime2 DEFAULT 0
        CONSTRAINT CK_Characters_AutoTime2 CHECK (AutoTime2 >= 0),                               -- aAutoTime2: per-real-minute-decrementing free auto-hunt allowance (Server/ts25zone/S07_MyGame04.cpp:787-823); 1440 (24h) granted at creation (Server/ts25login/S04_MyWork02.cpp:888)
    Zone241Time              INT                NOT NULL
        CONSTRAINT DF_Characters_Zone241Time DEFAULT 0
        CONSTRAINT CK_Characters_Zone241Time CHECK (Zone241Time >= 0),                           -- aZone241Time (Server/Header/Protocol/STRUCT.h:751-757's persisted avatar snapshot field, always wired as literal 0 today). First durable consumer: Rebirth-advancement Path B ("Max Rebirth", CZ_TRIBE_WORK_SEND tSort 11) unconditionally += 10 on precondition pass (Server/ts25zone/S04_MyWork02.cpp:11342-11390); see usp_Character_AdjustZone241Time for the write-path primitive.
    FlushSequence            BIGINT             NOT NULL
        CONSTRAINT DF_Characters_FlushSequence DEFAULT 0,                                        -- idempotent write-behind
    CreatedAtUtc             DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Characters_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc             DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Characters_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Characters PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT UQ_Characters_Name UNIQUE (Name),
    CONSTRAINT UQ_Characters_Account_Slot UNIQUE (AccountId, Slot),
    CONSTRAINT CK_Characters_Slot CHECK (Slot BETWEEN 0 AND 2),
    CONSTRAINT FK_Characters_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Characters_TeacherCharacter FOREIGN KEY (TeacherCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_Characters_StudentCharacter FOREIGN KEY (StudentCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT CK_Characters_TeacherNotSelf CHECK (TeacherCharacterId IS NULL OR TeacherCharacterId <> CharacterId),
    CONSTRAINT CK_Characters_StudentNotSelf CHECK (StudentCharacterId IS NULL OR StudentCharacterId <> CharacterId),
    INDEX IX_Characters_Account NONCLUSTERED (AccountId) INCLUDE (Slot, Name, Tribe, Level)
);
