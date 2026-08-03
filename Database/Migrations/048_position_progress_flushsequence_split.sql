
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('game.Characters') AND name = 'PositionFlushSequence')
    ALTER TABLE game.Characters ADD PositionFlushSequence BIGINT NOT NULL CONSTRAINT DF_Characters_PositionFlushSequence DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('game.Characters') AND name = 'ProgressFlushSequence')
    ALTER TABLE game.Characters ADD ProgressFlushSequence BIGINT NOT NULL CONSTRAINT DF_Characters_ProgressFlushSequence DEFAULT 0;
GO

UPDATE game.Characters
SET PositionFlushSequence = FlushSequence,
    ProgressFlushSequence = FlushSequence
WHERE PositionFlushSequence = 0
  AND ProgressFlushSequence = 0;
GO

CREATE OR ALTER PROCEDURE game.usp_Character_PersistBatch @Positions game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.MapId                 = s.MapId,
        c.PosX                  = s.PosX,
        c.PosY                  = s.PosY,
        c.PosZ                  = s.PosZ,
        c.Heading               = s.Heading,
        c.PositionFlushSequence = s.FlushSequence,
        c.UpdatedAtUtc          = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Positions AS s
                  ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.PositionFlushSequence; 
END;
GO

CREATE OR ALTER PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                                  @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

    UPDATE c
    SET c.ProgressFlushSequence = s.FlushSequence,
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
    WHERE s.FlushSequence > c.ProgressFlushSequence; 

    DELETE ci
    FROM game.CharacterCostumeSlots ci
    INNER JOIN @Applied a ON ci.CharacterId = a.CharacterId;

    INSERT INTO game.CharacterCostumeSlots (CharacterId, Slot, ItemId, EnchantValue, ExpireDate)
    SELECT cs.CharacterId, cs.Slot, cs.ItemId, cs.EnchantValue, cs.ExpireDate
    FROM @Costumes cs
    INNER JOIN @Applied a ON cs.CharacterId = a.CharacterId;
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
           GREATEST(c.FlushSequence, c.PositionFlushSequence, c.ProgressFlushSequence) AS FlushSequence,
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
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer,
           c.RankPoint,
           c.CloakLuckyBoxPity,
           c.CloakVariantBoxPity,
           c.MountVariantBoxPity,
           c.ProtectForRefine,
           c.ProtectForCostume,
           c.ProtectForDestroy2,
           c.LodRounds,
           c.StellarCoreExpireDate,
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy,
           c.PlayTime1,
           c.PlayTime3,
           c.HsbStoneRewardClaimed,
           c.TowerCpMilestoneCounter,
           c.WarriorPill,
           c.WarriorScroll,
           c.SilverTime,
           c.GoldTime,
           c.DoubleKillNumTime,
           c.DoubleKillExpTime,
           c.DoubleKillNumTime2
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
           Serial,
           XPos,
           YPos
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

CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntrySummary @CharacterId INT
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
           GREATEST(c.FlushSequence, c.PositionFlushSequence, c.ProgressFlushSequence) AS FlushSequence
    FROM game.Characters AS c
    WHERE c.CharacterId = @CharacterId;
END;
GO
