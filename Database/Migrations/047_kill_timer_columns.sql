-- Ajoute DoubleKillNumTime / DoubleKillExpTime / DoubleKillNumTime2 a game.Characters,
-- reconstruit game.tvp_CharacterProgress (93 colonnes) et les procedures write-behind
-- pour les scrolls de kill-bonus PvP (items 1118/1119/1120/1155/1163/1186/1228/1454/1456/8401/8402/8438).
--
-- DEPENDANCE : doit s'appliquer APRES 046_ornament_silver_gold_time_columns.sql
-- (qui etablit le TVP a 90 colonnes avec SilverTime/GoldTime/ProtectForDeath en queue).
-- Ce script extend le TVP de 90 a 93 colonnes.

-- 0. Ajouter les colonnes a game.Characters (idempotent).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('game.Characters') AND name = 'DoubleKillNumTime')
    ALTER TABLE game.Characters ADD DoubleKillNumTime INT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('game.Characters') AND name = 'DoubleKillExpTime')
    ALTER TABLE game.Characters ADD DoubleKillExpTime INT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('game.Characters') AND name = 'DoubleKillNumTime2')
    ALTER TABLE game.Characters ADD DoubleKillNumTime2 INT NOT NULL DEFAULT 0;
GO

-- 1. Dropper les procedures dependantes puis le type.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 2. Recreer le type avec les 3 nouvelles colonnes en queue (apres ProtectForDeath).
--    Le TVP doit refleter l'ordre exact de CharacterProgressTvp.cs.
--    93 colonnes au total.
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
    ImproveItemValue         INT          NOT NULL,
    AddItemValue             INT          NOT NULL,
    HighItemValue            INT          NOT NULL,
    TaiyanKeyTimer           INT          NOT NULL,
    RankPoint                INT          NOT NULL,
    CloakLuckyBoxPity        INT          NOT NULL,
    CloakVariantBoxPity      INT          NOT NULL,
    MountVariantBoxPity      INT          NOT NULL,
    ProtectForRefine         INT          NOT NULL,
    ProtectForDestroy        INT          NOT NULL,
    ProtectForCostume        INT          NOT NULL,
    ProtectForDestroy2       INT          NOT NULL,
    LodRounds                INT          NOT NULL,
    StellarCoreExpireDate    NVARCHAR(80) NOT NULL,
    EliteDungeonTime         INT          NOT NULL,
    DungeonKeyTime           INT          NOT NULL,
    IvyHallTicketTime        INT          NOT NULL,
    ScrollOfSeekersTime      INT          NOT NULL,
    FightingGodForDestroy    INT          NOT NULL,
    PetBagDate               INT          NOT NULL,
    PlayTime1                INT          NOT NULL,
    PlayTime3                INT          NOT NULL,
    HsbStoneRewardClaimed    INT          NOT NULL,
    TowerCpMilestoneCounter  INT          NOT NULL,
    InventoryDate            INT          NOT NULL,
    StoreDate                INT          NOT NULL,
    WarriorPill              INT          NOT NULL,
    WarriorScroll            INT          NOT NULL,
    SilverTime               INT          NOT NULL,
    GoldTime                 INT          NOT NULL,
    DoubleKillNumTime        INT          NOT NULL,
    DoubleKillExpTime        INT          NOT NULL,
    DoubleKillNumTime2       INT          NOT NULL,
    ProtectForDeath          INT          NOT NULL
);
GO

-- 3. usp_Character_PersistProgressBatch — flush periodique.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

    UPDATE c
    SET c.FlushSequence       = s.FlushSequence,
        c.Level                = s.Level,
        c.Level2               = s.Level2,
        c.Experience           = s.Experience,
        c.Life                 = s.Life,
        c.MaxLife              = s.MaxLife,
        c.Mana                 = s.Mana,
        c.MaxMana              = s.MaxMana,
        c.StatVit              = s.StatVit,
        c.StatStr              = s.StatStr,
        c.StatInt              = s.StatInt,
        c.StatDex              = s.StatDex,
        c.StatPoints           = s.StatPoints,
        c.SkillPoints          = s.SkillPoints,
        c.ContributionPoints   = s.ContributionPoints,
        c.Exp2                 = s.Exp2,
        c.RebirthCount         = s.RebirthCount,
        c.EatLifePotion        = s.EatLifePotion,
        c.EatManaPotion        = s.EatManaPotion,
        c.EatStrPotion         = s.EatStrPotion,
        c.EatDexPotion         = s.EatDexPotion,
        c.EatElePotion         = s.EatElePotion,
        c.DropItemTime         = s.DropItemTime,
        c.M15PetLuckyBoxPity   = s.M15PetLuckyBoxPity,
        c.MountItemId          = s.MountItemId,
        c.MountExpActivity     = s.MountExpActivity,
        c.MountPower           = s.MountPower,
        c.MountSlotIndex       = s.MountSlotIndex,
        c.MountTime            = s.MountTime,
        c.VisibleState         = s.VisibleState,
        c.SpecialState         = s.SpecialState,
        c.UseOrnament          = s.UseOrnament,
        c.Title                = s.Title,
        c.Halo                 = s.Halo,
        c.TeacherPoint         = s.TeacherPoint,
        c.WarPoint             = c.WarPoint + s.WarPointDelta,
        c.BloodCoin            = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time         = s.PetExpX2Time,
        c.AnimalAbsorbTime     = s.AnimalAbsorbTime,
        c.AnimalAbsorbState    = s.AnimalAbsorbState,
        c.CostumeIndex         = s.CostumeIndex,
        c.ProtectForHalo       = s.ProtectForHalo,
        c.BonusItemLevel       = s.BonusItemLevel,
        c.BonusItemValue       = s.BonusItemValue,
        c.TribeNotifyScrollCount = s.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = s.TribeFourReturnAllowance,
        c.BottleSlots          = s.BottleSlots,
        c.DrunkBottleIndex     = s.DrunkBottleIndex,
        c.AutoBuffTime         = s.AutoBuffTime,
        c.AutoBuffSkill        = s.AutoBuffSkill,
        c.RankPointDate        = s.RankPointDate,
        c.RankBuffType         = s.RankBuffType,
        c.AutoTime             = s.AutoTime,
        c.AutoTime2            = s.AutoTime2,
        c.BuffX2Time           = s.BuffX2Time,
        c.PremiumExpireUtc     = s.PremiumExpireUtc,
        c.PetGrowth            = s.PetGrowth,
        c.PetActivity          = s.PetActivity,
        c.ImproveItemValue     = s.ImproveItemValue,
        c.AddItemValue         = s.AddItemValue,
        c.HighItemValue        = s.HighItemValue,
        c.TaiyanKeyTimer       = s.TaiyanKeyTimer,
        c.RankPoint            = s.RankPoint,
        c.CloakLuckyBoxPity    = s.CloakLuckyBoxPity,
        c.CloakVariantBoxPity  = s.CloakVariantBoxPity,
        c.MountVariantBoxPity  = s.MountVariantBoxPity,
        c.ProtectForRefine     = s.ProtectForRefine,
        c.ProtectForDestroy    = s.ProtectForDestroy,
        c.ProtectForCostume    = s.ProtectForCostume,
        c.ProtectForDestroy2   = s.ProtectForDestroy2,
        c.LodRounds            = s.LodRounds,
        c.StellarCoreExpireDate = s.StellarCoreExpireDate,
        c.EliteDungeonTime     = s.EliteDungeonTime,
        c.DungeonKeyTime       = s.DungeonKeyTime,
        c.IvyHallTicketTime    = s.IvyHallTicketTime,
        c.ScrollOfSeekersTime  = s.ScrollOfSeekersTime,
        c.FightingGodForDestroy = s.FightingGodForDestroy,
        c.PetBagDate           = s.PetBagDate,
        c.PlayTime1            = s.PlayTime1,
        c.PlayTime3            = s.PlayTime3,
        c.HsbStoneRewardClaimed = s.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter = s.TowerCpMilestoneCounter,
        c.InventoryDate        = s.InventoryDate,
        c.StoreDate            = s.StoreDate,
        c.WarriorPill          = s.WarriorPill,
        c.WarriorScroll        = s.WarriorScroll,
        c.SilverTime           = s.SilverTime,
        c.GoldTime             = s.GoldTime,
        c.DoubleKillNumTime    = s.DoubleKillNumTime,
        c.DoubleKillExpTime    = s.DoubleKillExpTime,
        c.DoubleKillNumTime2   = s.DoubleKillNumTime2,
        c.ProtectForDeath      = s.ProtectForDeath,
        c.UpdatedAtUtc         = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied
    FROM game.Characters c
    INNER JOIN @Progress s ON c.CharacterId = s.CharacterId
    WHERE s.FlushSequence > c.FlushSequence;

    DELETE ci
    FROM game.CharacterCostumeSlots ci
    INNER JOIN @Applied a ON ci.CharacterId = a.CharacterId;

    INSERT INTO game.CharacterCostumeSlots (CharacterId, Slot, ItemId, EnchantValue, ExpireDate)
    SELECT cs.CharacterId, cs.Slot, cs.ItemId, cs.EnchantValue, cs.ExpireDate
    FROM @Costumes cs
    INNER JOIN @Applied a ON cs.CharacterId = a.CharacterId;
END;
GO

-- 4. usp_Character_PersistFinalFlush — flush terminal (deconnexion / changement de zone).
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress  game.tvp_CharacterProgress READONLY,
                                                      @Position  game.tvp_CharacterPosition  READONLY,
                                                      @Costumes  game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.FlushSequence       = p.FlushSequence,
        c.Level                = p.Level,
        c.Level2               = p.Level2,
        c.Experience           = p.Experience,
        c.Life                 = p.Life,
        c.MaxLife              = p.MaxLife,
        c.Mana                 = p.Mana,
        c.MaxMana              = p.MaxMana,
        c.StatVit              = p.StatVit,
        c.StatStr              = p.StatStr,
        c.StatInt              = p.StatInt,
        c.StatDex              = p.StatDex,
        c.StatPoints           = p.StatPoints,
        c.SkillPoints          = p.SkillPoints,
        c.ContributionPoints   = p.ContributionPoints,
        c.Exp2                 = p.Exp2,
        c.RebirthCount         = p.RebirthCount,
        c.EatLifePotion        = p.EatLifePotion,
        c.EatManaPotion        = p.EatManaPotion,
        c.EatStrPotion         = p.EatStrPotion,
        c.EatDexPotion         = p.EatDexPotion,
        c.EatElePotion         = p.EatElePotion,
        c.DropItemTime         = p.DropItemTime,
        c.M15PetLuckyBoxPity   = p.M15PetLuckyBoxPity,
        c.MountItemId          = p.MountItemId,
        c.MountExpActivity     = p.MountExpActivity,
        c.MountPower           = p.MountPower,
        c.MountSlotIndex       = p.MountSlotIndex,
        c.MountTime            = p.MountTime,
        c.VisibleState         = p.VisibleState,
        c.SpecialState         = p.SpecialState,
        c.UseOrnament          = p.UseOrnament,
        c.Title                = p.Title,
        c.Halo                 = p.Halo,
        c.TeacherPoint         = p.TeacherPoint,
        c.WarPoint             = c.WarPoint + p.WarPointDelta,
        c.BloodCoin            = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time         = p.PetExpX2Time,
        c.AnimalAbsorbTime     = p.AnimalAbsorbTime,
        c.AnimalAbsorbState    = p.AnimalAbsorbState,
        c.CostumeIndex         = p.CostumeIndex,
        c.ProtectForHalo       = p.ProtectForHalo,
        c.BonusItemLevel       = p.BonusItemLevel,
        c.BonusItemValue       = p.BonusItemValue,
        c.TribeNotifyScrollCount = p.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = p.TribeFourReturnAllowance,
        c.BottleSlots          = p.BottleSlots,
        c.DrunkBottleIndex     = p.DrunkBottleIndex,
        c.AutoBuffTime         = p.AutoBuffTime,
        c.AutoBuffSkill        = p.AutoBuffSkill,
        c.RankPointDate        = p.RankPointDate,
        c.RankBuffType         = p.RankBuffType,
        c.AutoTime             = p.AutoTime,
        c.AutoTime2            = p.AutoTime2,
        c.BuffX2Time           = p.BuffX2Time,
        c.PremiumExpireUtc     = p.PremiumExpireUtc,
        c.PetGrowth            = p.PetGrowth,
        c.PetActivity          = p.PetActivity,
        c.ImproveItemValue     = p.ImproveItemValue,
        c.AddItemValue         = p.AddItemValue,
        c.HighItemValue        = p.HighItemValue,
        c.TaiyanKeyTimer       = p.TaiyanKeyTimer,
        c.RankPoint            = p.RankPoint,
        c.CloakLuckyBoxPity    = p.CloakLuckyBoxPity,
        c.CloakVariantBoxPity  = p.CloakVariantBoxPity,
        c.MountVariantBoxPity  = p.MountVariantBoxPity,
        c.ProtectForRefine     = p.ProtectForRefine,
        c.ProtectForDestroy    = p.ProtectForDestroy,
        c.ProtectForCostume    = p.ProtectForCostume,
        c.ProtectForDestroy2   = p.ProtectForDestroy2,
        c.LodRounds            = p.LodRounds,
        c.StellarCoreExpireDate = p.StellarCoreExpireDate,
        c.EliteDungeonTime     = p.EliteDungeonTime,
        c.DungeonKeyTime       = p.DungeonKeyTime,
        c.IvyHallTicketTime    = p.IvyHallTicketTime,
        c.ScrollOfSeekersTime  = p.ScrollOfSeekersTime,
        c.FightingGodForDestroy = p.FightingGodForDestroy,
        c.PetBagDate           = p.PetBagDate,
        c.PlayTime1            = p.PlayTime1,
        c.PlayTime3            = p.PlayTime3,
        c.HsbStoneRewardClaimed = p.HsbStoneRewardClaimed,
        c.TowerCpMilestoneCounter = p.TowerCpMilestoneCounter,
        c.InventoryDate        = p.InventoryDate,
        c.StoreDate            = p.StoreDate,
        c.WarriorPill          = p.WarriorPill,
        c.WarriorScroll        = p.WarriorScroll,
        c.SilverTime           = p.SilverTime,
        c.GoldTime             = p.GoldTime,
        c.DoubleKillNumTime    = p.DoubleKillNumTime,
        c.DoubleKillExpTime    = p.DoubleKillExpTime,
        c.DoubleKillNumTime2   = p.DoubleKillNumTime2,
        c.ProtectForDeath      = p.ProtectForDeath,
        c.MapId                = pos.MapId,
        c.PosX                 = pos.PosX,
        c.PosY                 = pos.PosY,
        c.PosZ                 = pos.PosZ,
        c.Heading              = pos.Heading,
        c.UpdatedAtUtc         = SYSUTCDATETIME()
    FROM game.Characters c
    INNER JOIN @Progress p ON c.CharacterId = p.CharacterId
    INNER JOIN @Position pos ON c.CharacterId = pos.CharacterId
    WHERE p.FlushSequence >= c.FlushSequence;

    DELETE ci
    FROM game.CharacterCostumeSlots ci
    WHERE ci.CharacterId IN (SELECT CharacterId FROM @Progress);

    INSERT INTO game.CharacterCostumeSlots (CharacterId, Slot, ItemId, EnchantValue, ExpireDate)
    SELECT cs.CharacterId, cs.Slot, cs.ItemId, cs.EnchantValue, cs.ExpireDate
    FROM @Costumes cs
    WHERE EXISTS (SELECT 1 FROM @Progress p WHERE p.CharacterId = cs.CharacterId);
END;
GO

-- 5. usp_Character_GetForWorldEntry : ajouter les 3 colonnes en queue de RS0.
CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @AccountId INT, @Slot TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    -- RS0 : colonnes du personnage (CharacterWorldSnapshotDto — append seulement en queue)
    SELECT c.CharacterId,
           c.Name,
           c.Tribe,
           c.PreviousTribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.Level2,
           c.Experience,
           c.Exp2,
           c.RebirthCount,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.ContributionPoints,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Title,
           c.Halo,
           c.TeacherPoint,
           c.WarPoint,
           c.BloodCoin,
           c.AutoBuffTime,
           c.AutoBuffSkill,
           c.AutoTime,
           c.AutoTime2,
           c.DropItemTime,
           c.M15PetLuckyBoxPity,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AnimalAbsorbTime,
           c.AnimalAbsorbState,
           c.CostumeIndex,
           c.BonusItemLevel,
           c.BonusItemValue,
           c.VisibleState,
           c.SpecialState,
           c.UseOrnament,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForHalo,
           c.ProtectForRefine,
           c.ProtectForDestroy,
           c.ProtectForCostume,
           c.ProtectForDestroy2,
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer,
           c.RankPoint,
           c.RankPointDate,
           c.RankBuffType,
           c.CloakLuckyBoxPity,
           c.CloakVariantBoxPity,
           c.MountVariantBoxPity,
           c.Zone241Time,
           c.LodRounds,
           c.TribeNotifyScrollCount,
           c.TribeFourReturnAllowance,
           c.BottleSlots,
           c.DrunkBottleIndex,
           c.StellarCoreExpireDate,
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy,
           c.PetBagDate,
           c.PlayTime1,
           c.PlayTime3,
           c.HsbStoneRewardClaimed,
           c.TowerCpMilestoneCounter,
           c.InventoryDate,
           c.StoreDate,
           c.WarriorPill,
           c.WarriorScroll,
           c.BuffX2Time,
           c.PremiumExpireUtc,
           c.PetGrowth,
           c.PetActivity,
           c.AutoHuntConfig,
           c.AutoHuntEnabled,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.IsMuted,
           c.UserSort,
           c.AccountId,
           c.TribeRole,
           c.Zone241Time                 AS Zone241Time2,
           c.AutoPotionLifeThreshold,
           c.AutoPotionManaThreshold,
           c.TaiyanKeyTimer              AS Zone125Time,
           c.IvyHallTicketTime           AS Zone050Time2,
           c.ProtectForDeath,
           c.SilverTime,
           c.GoldTime,
           c.DoubleKillNumTime,
           c.DoubleKillExpTime,
           c.DoubleKillNumTime2
    FROM game.Characters c
    WHERE c.AccountId = @AccountId
      AND c.Slot = @Slot;

    -- RS1-RS4 : items, skills, hotkeys, buffs (inchanges)
    SELECT ci.Container, ci.Slot, ci.ItemId, ci.Quantity, ci.Enchant, ci.Combine, ci.Refine, ci.Socket, ci.Serial,
           ci.XPos, ci.YPos
    FROM game.CharacterItems ci
    WHERE ci.CharacterId = (SELECT CharacterId FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot);

    SELECT cs.Slot, cs.SkillId, cs.Grade
    FROM game.CharacterSkills cs
    WHERE cs.CharacterId = (SELECT CharacterId FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot);

    SELECT ck.Page, ck.SlotIndex, ck.Value1, ck.Value2, ck.Kind
    FROM game.CharacterHotkeys ck
    WHERE ck.CharacterId = (SELECT CharacterId FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot);

    SELECT cb.Slot, cb.Value1, cb.Value2
    FROM game.CharacterBuffs cb
    WHERE cb.CharacterId = (SELECT CharacterId FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot);
END;
GO
