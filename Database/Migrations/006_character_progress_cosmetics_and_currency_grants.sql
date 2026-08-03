
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId        INT      NOT NULL,
    FlushSequence      BIGINT   NOT NULL,
    Level              SMALLINT NOT NULL,
    Level2             SMALLINT NOT NULL,
    Experience         BIGINT   NOT NULL,
    Life               INT      NOT NULL,
    MaxLife            INT      NOT NULL,
    Mana               INT      NOT NULL,
    MaxMana            INT      NOT NULL,
    StatVit            INT      NOT NULL,
    StatStr            INT      NOT NULL,
    StatInt            INT      NOT NULL,
    StatDex            INT      NOT NULL,
    StatPoints         INT      NOT NULL,
    SkillPoints        INT      NOT NULL,
    ContributionPoints INT      NOT NULL,
    Exp2               INT      NOT NULL,
    RebirthCount       INT      NOT NULL,
    EatLifePotion      INT      NOT NULL,
    EatManaPotion      INT      NOT NULL,
    EatStrPotion       INT      NOT NULL,
    EatDexPotion       INT      NOT NULL,
    EatElePotion       INT      NOT NULL,
    DropItemTime       INT      NOT NULL,
    M15PetLuckyBoxPity INT      NOT NULL,
    MountItemId        INT      NOT NULL,
    MountExpActivity   INT      NOT NULL,
    MountPower         INT      NOT NULL,
    MountSlotIndex     INT      NOT NULL,
    MountTime          INT      NOT NULL,
    VisibleState       INT      NOT NULL,
    SpecialState       INT      NOT NULL,
    UseOrnament        INT      NOT NULL,
    Title              INT      NOT NULL,
    Halo               INT      NOT NULL,
    TeacherPoint       INT      NOT NULL,
    WarPointDelta      INT      NOT NULL,
    BloodCoinDelta     INT      NOT NULL
);
GO

CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level              = s.Level,
        c.Level2             = s.Level2,
        c.Experience         = s.Experience,
        c.Life               = s.Life,
        c.MaxLife            = s.MaxLife,
        c.Mana               = s.Mana,
        c.MaxMana            = s.MaxMana,
        c.StatVit            = s.StatVit,
        c.StatStr            = s.StatStr,
        c.StatInt            = s.StatInt,
        c.StatDex            = s.StatDex,
        c.StatPoints         = s.StatPoints,
        c.SkillPoints        = s.SkillPoints,
        c.ContributionPoints = s.ContributionPoints,
        c.Exp2               = s.Exp2,
        c.RebirthCount       = s.RebirthCount,
        c.EatLifePotion      = s.EatLifePotion,
        c.EatManaPotion      = s.EatManaPotion,
        c.EatStrPotion       = s.EatStrPotion,
        c.EatDexPotion       = s.EatDexPotion,
        c.EatElePotion       = s.EatElePotion,
        c.DropItemTime       = s.DropItemTime,
        c.M15PetLuckyBoxPity = s.M15PetLuckyBoxPity,
        c.MountItemId        = s.MountItemId,
        c.MountExpActivity   = s.MountExpActivity,
        c.MountPower         = s.MountPower,
        c.MountSlotIndex     = s.MountSlotIndex,
        c.MountTime          = s.MountTime,
        c.VisibleState       = s.VisibleState,
        c.SpecialState       = s.SpecialState,
        c.UseOrnament        = s.UseOrnament,
        c.Title              = s.Title,
        c.Halo               = s.Halo,
        c.TeacherPoint       = s.TeacherPoint,
        c.WarPoint           = c.WarPoint + s.WarPointDelta,
        c.BloodCoin          = c.BloodCoin + s.BloodCoinDelta,
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; 
END;
GO

CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level              = p.Level,
        c.Level2             = p.Level2,
        c.Experience         = p.Experience,
        c.Life               = p.Life,
        c.MaxLife            = p.MaxLife,
        c.Mana               = p.Mana,
        c.MaxMana            = p.MaxMana,
        c.StatVit            = p.StatVit,
        c.StatStr            = p.StatStr,
        c.StatInt            = p.StatInt,
        c.StatDex            = p.StatDex,
        c.StatPoints         = p.StatPoints,
        c.SkillPoints        = p.SkillPoints,
        c.ContributionPoints = p.ContributionPoints,
        c.Exp2               = p.Exp2,
        c.RebirthCount       = p.RebirthCount,
        c.EatLifePotion      = p.EatLifePotion,
        c.EatManaPotion      = p.EatManaPotion,
        c.EatStrPotion       = p.EatStrPotion,
        c.EatDexPotion       = p.EatDexPotion,
        c.EatElePotion       = p.EatElePotion,
        c.DropItemTime       = p.DropItemTime,
        c.M15PetLuckyBoxPity = p.M15PetLuckyBoxPity,
        c.MountItemId        = p.MountItemId,
        c.MountExpActivity   = p.MountExpActivity,
        c.MountPower         = p.MountPower,
        c.MountSlotIndex     = p.MountSlotIndex,
        c.MountTime          = p.MountTime,
        c.VisibleState       = p.VisibleState,
        c.SpecialState       = p.SpecialState,
        c.UseOrnament        = p.UseOrnament,
        c.Title              = p.Title,
        c.Halo               = p.Halo,
        c.TeacherPoint       = p.TeacherPoint,
        c.WarPoint           = c.WarPoint + p.WarPointDelta,
        c.BloodCoin          = c.BloodCoin + p.BloodCoinDelta,
        c.MapId              = q.MapId,
        c.PosX               = q.PosX,
        c.PosY               = q.PosY,
        c.PosZ               = q.PosZ,
        c.Heading            = q.Heading,
        c.FlushSequence      = q.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; 
END;
GO

DROP PROCEDURE IF EXISTS game.usp_Character_GetForWorldEntry;
GO

CREATE PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
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
           c.BloodCoin
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
