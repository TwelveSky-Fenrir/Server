-- Lot 2 -- mount attribute write-behind persistence.
--
-- Folds the single persisted mount block into the write-behind progress flush so in-session mount play
-- (activity spend, exp gain, attribute rolls -- all mutated in PlayerRuntimeState) is re-encoded and persisted
-- with the rest of the avatar instead of silently reverting to its last-persisted value on the next flush,
-- the same gap the DropItemTime/Eat*Potion counters already closed. game.tvp_CharacterProgress gains five
-- trailing INT columns -- MountItemId / MountExpActivity (packed activity*1e6+exp) / MountPower (packed 8 rolled
-- digits) / MountSlotIndex (the aAnimalIndex pointer) / MountTime -- and both write-behind procedures gain the
-- matching SET clauses. The destination columns already exist on game.Characters (base CREATE TABLE), so no
-- table change is needed here.
--
-- WHY A NEW MIGRATION SCRIPT INSTEAD OF EDITING THE BASE FILES
-- Schemas/Types/game/tvp_CharacterProgress.sql, StoredProcedures/game/usp_Character_PersistProgressBatch.sql,
-- and StoredProcedures/game/usp_Character_PersistFinalFlush.sql are already listed in _manifest.txt and are
-- SHA-256-journaled by Tools/Fenrir.Tools.DbMigrator on every database that has ever applied them (the AppHost
-- dev volume is persistent). The migrator refuses to re-apply a journaled path whose content changed, so an
-- in-place edit would hard-fail the next run on any provisioned database. This additive change therefore lands
-- as a brand-new script: a FRESH database applies the base scripts first (25-column TVP + mount-less procs),
-- then this migration converts them to the final shape; a PERSISTENT database skips the already-journaled base
-- scripts and applies only this. Both converge on the identical mount-carrying shape. Base files stay at their
-- original shape by design; the "current" definition of these objects is base + this migration.
--
-- WHY DROP+RECREATE THE TYPE
-- A TABLE type cannot be ALTERed to add columns, and it cannot be dropped while any procedure references it as
-- a parameter. The two write-behind procedures both take @Progress game.tvp_CharacterProgress READONLY, so they
-- are dropped first, the type is dropped+recreated with the five new columns, then both procedures are
-- recreated. usp_HeroRanking_ClaimReward references game.tvp_CharacterProgress in a header COMMENT only (its
-- three parameters are all scalars -- no TVP), so it is not a real dependency and is deliberately left alone.
--
-- EXECUTE permissions are unaffected: Schemas/002_roles.sql grants EXECUTE at the SCHEMA level
-- (GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role), a covering permission inherited transitively by every
-- object created in the schema, now and in the future (Microsoft Learn, "Get started with Database Engine
-- permissions"). Neither persist procedure carries an object-level grant, so dropping+recreating them loses no
-- permission; the grant lives on the schema, not the object.

-- 1. Drop both procedures that reference the type (they block DROP TYPE), then the type itself. IF EXISTS keeps
--    this resilient regardless of start state.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 2. Recreate the type with the five trailing Mount* columns. Body is identical to
--    Schemas/Types/game/tvp_CharacterProgress.sql plus the appended columns.
-- RebirthCount/M15PetLuckyBoxPity/Eat*Potion stay INT here even though game.Characters narrowed them to
-- TINYINT/SMALLINT (buffer-pool page-density fix) -- this TVP is a transient client-side shape, not a stored
-- row, and SQL Server converts the wider value into the narrower destination column on write.
-- Mount* appended last: the single persisted mount (garage slot 0) re-encoded from PlayerRuntimeState on each
-- write-behind flush -- MountExpActivity packs activity/accumulated-exp, MountPower packs the 8 rolled digits,
-- MountSlotIndex is the aAnimalIndex pointer, MountTime the mount timer. Same "without it here the flush would
-- silently revert a live counter" reasoning as the DropItemTime/Eat*Potion columns above them.
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
    MountTime          INT      NOT NULL
);
GO

-- 3. Recreate usp_Character_PersistProgressBatch with the five Mount* SET clauses appended before the
--    FlushSequence/UpdatedAtUtc writes. Idempotence guard (strictly-greater FlushSequence) unchanged.
-- Idempotent: same strictly-greater FlushSequence guard as usp_Character_PersistBatch (one shared
-- monotonic sequence per character across both flush flavors).
-- Money/BigMoney intentionally absent: balances only move through usp_Character_AdjustMoney, to
-- avoid a last-write-wins flush clobbering a balance. Mount* are safe here (single-owner PlayerRuntimeState
-- mount state, re-encoded from the runtime arrays) -- same category as DropItemTime/Eat*Potion, not Money.
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
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 4. Recreate usp_Character_PersistFinalFlush with the same five Mount* SET clauses. Progress + position are
--    written in one UPDATE so the two halves of a logout snapshot cannot be split by a mid-sequence failure.
-- Single-character, single-statement counterpart to usp_Character_PersistProgressBatch +
-- usp_Character_PersistBatch, used only by the disconnect-time immediate flush
-- (PositionWriteBehindHost.FlushCharacterNowAsync). Same strictly-greater FlushSequence idempotence guard;
-- both TVPs carry one row each, stamped with the same FlushSequence value by the caller.
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
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO
