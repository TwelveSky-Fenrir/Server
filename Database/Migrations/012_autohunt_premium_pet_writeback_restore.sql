-- Regression fix. Migration 010 rebuilt game.tvp_CharacterProgress from Migration 007's 37-column form
-- instead of Migration 008's 42-column form, silently dropping 11 columns (5 restored by Migration 011:
-- Title/Halo/TeacherPoint/WarPointDelta/BloodCoinDelta). The other 6 -- AutoTime/AutoTime2/BuffX2Time/
-- PremiumExpireUtc/PetGrowth/PetActivity -- were never restored and stayed unreachable by the write-behind
-- TVP path. game.Characters already carries all 6 columns (base table + Migration 008 ALTER); no ALTER
-- TABLE is needed here, only the type and its two consuming procedures.
--
-- New script, not an edit: the base type/procedure files and Migrations 008/010/011 are already
-- SHA-256-journaled. Same DROP-procs / DROP-type / CREATE-type / CREATE-procs sequence as those scripts.

DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- Mirrors Fenrir.Data.Abstractions.Characters.CharacterProgressTvp field order exactly (59 columns): the
-- 53 left by Migration 011, plus the 6 lost columns restored here in queue.
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
    PetActivity              INT          NOT NULL
);
GO

-- Periodic flush. Idempotence guard (FlushSequence strictly increasing) unchanged. The 6 restored columns
-- are absolute writes, same ownership class as the other PlayerRuntimeState-owned counters -- no concurrent
-- writer (unlike WarPoint/BloodCoin, which stay delta).
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
        c.FlushSequence            = s.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- Terminal flush -- disconnect (GameConnectionHost) and zone change (ZoneMoveService), both via
-- PositionWriteBehindHost.FlushCharacterNowAsync. Progression and position in one UPDATE, unchanged.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.FlushSequence            = q.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO
