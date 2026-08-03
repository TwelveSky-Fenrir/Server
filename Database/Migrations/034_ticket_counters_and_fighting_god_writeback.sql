
IF COL_LENGTH('game.Characters', 'EliteDungeonTime') IS NULL
ALTER TABLE game.Characters
    ADD EliteDungeonTime INT NOT NULL
            CONSTRAINT DF_Characters_EliteDungeonTime DEFAULT 0
            CONSTRAINT CK_Characters_EliteDungeonTime CHECK (EliteDungeonTime >= 0),
        DungeonKeyTime INT NOT NULL
            CONSTRAINT DF_Characters_DungeonKeyTime DEFAULT 0
            CONSTRAINT CK_Characters_DungeonKeyTime CHECK (DungeonKeyTime >= 0),
        IvyHallTicketTime INT NOT NULL
            CONSTRAINT DF_Characters_IvyHallTicketTime DEFAULT 0
            CONSTRAINT CK_Characters_IvyHallTicketTime CHECK (IvyHallTicketTime >= 0),
        ScrollOfSeekersTime INT NOT NULL
            CONSTRAINT DF_Characters_ScrollOfSeekersTime DEFAULT 0
            CONSTRAINT CK_Characters_ScrollOfSeekersTime CHECK (ScrollOfSeekersTime >= 0),
        FightingGodForDestroy INT NOT NULL
            CONSTRAINT DF_Characters_FightingGodForDestroy DEFAULT 0
            CONSTRAINT CK_Characters_FightingGodForDestroy CHECK (FightingGodForDestroy >= 0);
GO

DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId              INT          NOT NULL,
    FlushSequence            BIGINT       NOT NULL,
    Level                    SMALLINT     NOT NULL,
    Level2                   SMALLINT     NOT NULL,
    Experience               BIGINT       NOT NULL,
    Life                     INT          NOT NULL,
    MaxLife                  INT          NOT NULL,
    Mana                     INT          NOT NULL,
    MaxMana                  INT          NOT NULL,
    StatVit                  INT          NOT NULL,
    StatStr                  INT          NOT NULL,
    StatInt                  INT          NOT NULL,
    StatDex                  INT          NOT NULL,
    StatPoints               INT          NOT NULL,
    SkillPoints              INT          NOT NULL,
    ContributionPoints       INT          NOT NULL,
    Exp2                     INT          NOT NULL,
    RebirthCount             INT          NOT NULL,
    EatLifePotion            INT          NOT NULL,
    EatManaPotion            INT          NOT NULL,
    EatStrPotion             INT          NOT NULL,
    EatDexPotion             INT          NOT NULL,
    EatElePotion             INT          NOT NULL,
    DropItemTime             INT          NOT NULL,
    M15PetLuckyBoxPity       INT          NOT NULL,
    MountItemId              INT          NOT NULL,
    MountExpActivity         INT          NOT NULL,
    MountPower               INT          NOT NULL,
    MountSlotIndex           INT          NOT NULL,
    MountTime                INT          NOT NULL,
    VisibleState             INT          NOT NULL,
    SpecialState             INT          NOT NULL,
    UseOrnament              INT          NOT NULL,
    Title                    INT          NOT NULL,
    Halo                     INT          NOT NULL,
    TeacherPoint             INT          NOT NULL,
    WarPointDelta            INT          NOT NULL,
    BloodCoinDelta           INT          NOT NULL,
    PetExpX2Time             INT          NOT NULL,
    AnimalAbsorbTime         INT          NOT NULL,
    AnimalAbsorbState        INT          NOT NULL,
    CostumeIndex             INT          NOT NULL,
    ProtectForHalo           INT          NOT NULL,
    BonusItemLevel           INT          NOT NULL,
    BonusItemValue           BIT          NOT NULL,
    TribeNotifyScrollCount   INT          NOT NULL,
    TribeFourReturnAllowance INT          NOT NULL,
    BottleSlots              NVARCHAR(70) NOT NULL,
    DrunkBottleIndex         INT          NOT NULL,
    AutoBuffTime             INT          NOT NULL,
    AutoBuffSkill            NVARCHAR(48) NOT NULL,
    RankPointDate            INT          NOT NULL,
    RankBuffType             INT          NOT NULL,
    AutoTime                 INT          NOT NULL,
    AutoTime2                INT          NOT NULL,
    BuffX2Time               INT          NOT NULL,
    PremiumExpireUtc         BIGINT       NOT NULL,
    PetGrowth                INT          NOT NULL,
    PetActivity              INT          NOT NULL,
    EliteDungeonTime         INT          NOT NULL,
    DungeonKeyTime           INT          NOT NULL,
    IvyHallTicketTime        INT          NOT NULL,
    ScrollOfSeekersTime      INT          NOT NULL,
    FightingGodForDestroy    INT          NOT NULL
);
GO

CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE
                     (
                         CharacterId INT NOT NULL PRIMARY KEY
                     );

    BEGIN TRANSACTION;

    UPDATE c
    SET c.Level                    = s.Level,
        c.Level2                   = s.Level2,
        c.Experience               = s.Experience,
        c.Life                     = s.Life,
        c.MaxLife                  = s.MaxLife,
        c.Mana                     = s.Mana,
        c.MaxMana                  = s.MaxMana,
        c.StatVit                  = s.StatVit,
        c.StatStr                  = s.StatStr,
        c.StatInt                  = s.StatInt,
        c.StatDex                  = s.StatDex,
        c.StatPoints               = s.StatPoints,
        c.SkillPoints              = s.SkillPoints,
        c.ContributionPoints       = s.ContributionPoints,
        c.Exp2                     = s.Exp2,
        c.RebirthCount             = s.RebirthCount,
        c.EatLifePotion            = s.EatLifePotion,
        c.EatManaPotion            = s.EatManaPotion,
        c.EatStrPotion             = s.EatStrPotion,
        c.EatDexPotion             = s.EatDexPotion,
        c.EatElePotion             = s.EatElePotion,
        c.DropItemTime             = s.DropItemTime,
        c.M15PetLuckyBoxPity       = s.M15PetLuckyBoxPity,
        c.MountItemId              = s.MountItemId,
        c.MountExpActivity         = s.MountExpActivity,
        c.MountPower               = s.MountPower,
        c.MountSlotIndex           = s.MountSlotIndex,
        c.MountTime                = s.MountTime,
        c.VisibleState             = s.VisibleState,
        c.SpecialState             = s.SpecialState,
        c.UseOrnament              = s.UseOrnament,
        c.Title                    = s.Title,
        c.Halo                     = s.Halo,
        c.TeacherPoint             = s.TeacherPoint,
        c.WarPoint                 = c.WarPoint + s.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time             = s.PetExpX2Time,
        c.AnimalAbsorbTime         = s.AnimalAbsorbTime,
        c.AnimalAbsorbState        = s.AnimalAbsorbState,
        c.CostumeIndex             = s.CostumeIndex,
        c.ProtectForHalo           = s.ProtectForHalo,
        c.BonusItemLevel           = s.BonusItemLevel,
        c.BonusItemValue           = s.BonusItemValue,
        c.TribeNotifyScrollCount   = s.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = s.TribeFourReturnAllowance,
        c.BottleSlots              = s.BottleSlots,
        c.DrunkBottleIndex         = s.DrunkBottleIndex,
        c.AutoBuffTime             = s.AutoBuffTime,
        c.AutoBuffSkill            = s.AutoBuffSkill,
        c.RankPointDate            = s.RankPointDate,
        c.RankBuffType             = s.RankBuffType,
        c.AutoTime                 = s.AutoTime,
        c.AutoTime2                = s.AutoTime2,
        c.BuffX2Time               = s.BuffX2Time,
        c.PremiumExpireUtc         = s.PremiumExpireUtc,
        c.PetGrowth                = s.PetGrowth,
        c.PetActivity              = s.PetActivity,
        c.EliteDungeonTime         = s.EliteDungeonTime,
        c.DungeonKeyTime           = s.DungeonKeyTime,
        c.IvyHallTicketTime        = s.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = s.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = s.FightingGodForDestroy,
        c.FlushSequence            = s.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; 

    DELETE cc
    FROM game.CharacterCostumes AS cc
             JOIN @Applied AS a ON a.CharacterId = cc.CharacterId;

    INSERT INTO game.CharacterCostumes (CharacterId, Slot, ItemId, ItemDate, ExpireDate)
    SELECT s.CharacterId,
           s.Slot,
           s.ItemId,
           s.ItemDate,
           s.ExpireDate
    FROM @Costumes AS s
             JOIN @Applied AS a ON a.CharacterId = s.CharacterId;

    COMMIT TRANSACTION;
END;
GO

CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY,
                                                      @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE
                     (
                         CharacterId INT NOT NULL PRIMARY KEY
                     );

    BEGIN TRANSACTION;

    UPDATE c
    SET c.Level                    = p.Level,
        c.Level2                   = p.Level2,
        c.Experience               = p.Experience,
        c.Life                     = p.Life,
        c.MaxLife                  = p.MaxLife,
        c.Mana                     = p.Mana,
        c.MaxMana                  = p.MaxMana,
        c.StatVit                  = p.StatVit,
        c.StatStr                  = p.StatStr,
        c.StatInt                  = p.StatInt,
        c.StatDex                  = p.StatDex,
        c.StatPoints               = p.StatPoints,
        c.SkillPoints              = p.SkillPoints,
        c.ContributionPoints       = p.ContributionPoints,
        c.Exp2                     = p.Exp2,
        c.RebirthCount             = p.RebirthCount,
        c.EatLifePotion            = p.EatLifePotion,
        c.EatManaPotion            = p.EatManaPotion,
        c.EatStrPotion             = p.EatStrPotion,
        c.EatDexPotion             = p.EatDexPotion,
        c.EatElePotion             = p.EatElePotion,
        c.DropItemTime             = p.DropItemTime,
        c.M15PetLuckyBoxPity       = p.M15PetLuckyBoxPity,
        c.MountItemId              = p.MountItemId,
        c.MountExpActivity         = p.MountExpActivity,
        c.MountPower               = p.MountPower,
        c.MountSlotIndex           = p.MountSlotIndex,
        c.MountTime                = p.MountTime,
        c.VisibleState             = p.VisibleState,
        c.SpecialState             = p.SpecialState,
        c.UseOrnament              = p.UseOrnament,
        c.Title                    = p.Title,
        c.Halo                     = p.Halo,
        c.TeacherPoint             = p.TeacherPoint,
        c.WarPoint                 = c.WarPoint + p.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time             = p.PetExpX2Time,
        c.AnimalAbsorbTime         = p.AnimalAbsorbTime,
        c.AnimalAbsorbState        = p.AnimalAbsorbState,
        c.CostumeIndex             = p.CostumeIndex,
        c.ProtectForHalo           = p.ProtectForHalo,
        c.BonusItemLevel           = p.BonusItemLevel,
        c.BonusItemValue           = p.BonusItemValue,
        c.TribeNotifyScrollCount   = p.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = p.TribeFourReturnAllowance,
        c.BottleSlots              = p.BottleSlots,
        c.DrunkBottleIndex         = p.DrunkBottleIndex,
        c.AutoBuffTime             = p.AutoBuffTime,
        c.AutoBuffSkill            = p.AutoBuffSkill,
        c.RankPointDate            = p.RankPointDate,
        c.RankBuffType             = p.RankBuffType,
        c.AutoTime                 = p.AutoTime,
        c.AutoTime2                = p.AutoTime2,
        c.BuffX2Time               = p.BuffX2Time,
        c.PremiumExpireUtc         = p.PremiumExpireUtc,
        c.PetGrowth                = p.PetGrowth,
        c.PetActivity              = p.PetActivity,
        c.EliteDungeonTime         = p.EliteDungeonTime,
        c.DungeonKeyTime           = p.DungeonKeyTime,
        c.IvyHallTicketTime        = p.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = p.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = p.FightingGodForDestroy,
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.FlushSequence            = q.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; 

    DELETE cc
    FROM game.CharacterCostumes AS cc
             JOIN @Applied AS a ON a.CharacterId = cc.CharacterId;

    INSERT INTO game.CharacterCostumes (CharacterId, Slot, ItemId, ItemDate, ExpireDate)
    SELECT s.CharacterId,
           s.Slot,
           s.ItemId,
           s.ItemDate,
           s.ExpireDate
    FROM @Costumes AS s
             JOIN @Applied AS a ON a.CharacterId = s.CharacterId;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence,
           c.Experience,
           c.Level2,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.Money,
           c.BigMoney,
           c.StoreMoney,
           c.BigStoreMoney,
           c.RebirthCount,
           c.Title,
           c.Halo,
           c.ContributionPoints,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForDeath,
           c.ProtectForDestroy,
           c.DoubleExpTime1,
           c.DoubleExpTime2,
           c.DropItemTime,
           c.InventoryDate,
           c.StoreDate,
           ISNULL(q.StepPermanent, 0) AS QuestStepPermanent,
           ISNULL(q.ActiveQuestId, 0) AS QuestActiveId,
           ISNULL(q.QSort, 0)         AS QuestSort,
           ISNULL(q.TargetPhase, 0)   AS QuestTargetPhase,
           ISNULL(q.KillCounter, 0)   AS QuestKillCounter,
           c.JoinWar,
           c.MissionKillOtherTribe,
           c.MissionKillMonster,
           c.MissionPlayTime,
           c.AutoHuntEnabled,
           c.AutoHuntConfig,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.PetGrowth,
           c.PetActivity,
           c.TeacherPoint,
           c.AutoBuffTime,
           c.PremiumExpireUtc,
           c.Exp2,
           c.PreviousTribe,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AutoTime2,
           c.Zone241Time,
           c.PetBagDate,
           c.WarPoint,
           c.M15PetLuckyBoxPity,
           c.VisibleState,
           c.SpecialState,
           c.UseOrnament,
           c.BloodCoin,
           c.PetExpX2Time,
           c.AnimalAbsorbTime,
           c.AnimalAbsorbState,
           c.CostumeIndex,
           c.ProtectForHalo,
           c.BonusItemLevel,
           c.BonusItemValue,
           c.TribeNotifyScrollCount,
           c.TribeFourReturnAllowance,
           c.BottleSlots,
           c.DrunkBottleIndex,
           c.AutoBuffSkill,
           c.RankPointDate,
           c.RankBuffType,
           c.AutoTime,
           c.BuffX2Time,
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           CAST(Quantity AS INT) AS Quantity, 
           Enchant,                           
           Combine,                           
           Refine,                            
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
    ORDER BY Container, Slot;

    SELECT SlotIndex,
           SkillId,
           Grade
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;

    SELECT Page,
           KeyIndex,
           Sort,
           Value1,
           Value2
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
    ORDER BY Page, KeyIndex;

    SELECT SlotIndex,
           Value,
           RemainingLegacyTicks
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
GO
