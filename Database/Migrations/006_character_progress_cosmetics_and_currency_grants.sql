-- Lot 1 -- persistence for five in-session-mutated avatar fields that survived nothing.
--
-- None of Title / Halo / TeacherPoint / WarPoint / BloodCoin had a write path, so every gain was lost on
-- disconnect AND on zone change (a zone change is a full reconnect: ZoneMoveService mints a handoff ticket
-- and the client reopens a session on the target zone's port, so PlayerRuntimeState is rebuilt from
-- game.Characters every time). The legacy persists all five:
--   aTitle        Server/Header/Protocol/STRUCT.h:405, FIELD_AVATAR0 with no #ifdef guard
--                 Server/Header/CSQLAvatar.cpp:617 (USE_TITLE, Server/Header/Protocol/DEFINE.h:81)
--   aHalo         STRUCT.h:407, CSQLAvatar.cpp:619 -- USE_HALO is defined at DEFINE.h:81 and the file's only
--                 #undef USE_HALO is commented out (DEFINE.h:108), so it is on in ReleaseM33 and ReleaseEU33
--   aTeacherPoint STRUCT.h:370, CSQLAvatar.cpp:588 (its neighbours aTeacherPointEvent/aTeacherPointDate are
--                 absent from CreateAvatarColumn and stay ephemeral -- do not persist them by symmetry)
--   aWarPoint     STRUCT.h:554, CSQLAvatar.cpp:685-686 under USE_WAR_POINT_SYSTEM, which is defined at
--                 DEFINE.h:125 inside #ifdef LNW33 and OUTSIDE the #ifndef LNW33EU sub-block (DEFINE.h:110-114,
--                 which holds only #undef MAX_IMPROVE_150) -- on in both shipped configurations
--   aBloodCoin    STRUCT.h:476, CSQLAvatar.cpp:683 with no guard (USE_BLOOD, DEFINE.h:50, defined
--                 unconditionally outside the #ifdef M33 block)
--
-- All five destination columns already exist on game.Characters (base CREATE TABLE), so this script adds no
-- column and there is no default to backfill onto existing rows.
--
-- TWO WRITE REGIMES, AND WHY
-- Title/Halo/TeacherPoint are written ABSOLUTE. No procedure writes those three columns today; the runtime
-- PlayerRuntimeState is their sole owner, exactly like ContributionPoints already in this TVP. A
-- last-write-wins flush is safe there.
--
-- WarPoint/BloodCoin are written as a DELTA (X = X + s.XDelta). Both columns already have an atomic
-- spend-side write path -- game.usp_Character_BuyWarPointItem (WarPoint = WarPoint - @WarPointCost) and
-- game.usp_Character_SpendBloodCoinAndReplaceContainer (BloodCoin = BloodCoin + @DeltaBloodCoin) -- and
-- neither touches FlushSequence nor mirrors the new balance back into the runtime state. An absolute
-- write-behind value would therefore restore already-spent points whenever a flush is captured before a spend
-- commits and lands after it: a currency duplication, precisely the reason Money/BigMoney are kept out of this
-- TVP (see the procedure's own header). A relative credit commutes with a relative debit under any
-- interleaving. That is also why this extends the existing TVP instead of opening a parallel credit procedure:
-- two write paths on one column diverge. The C# side (PlayerRuntimeState.PersistedWarPoint /
-- PersistedBloodCoin) only advances its baseline once SQL accepted the credit, so a failed flush replays the
-- same grant instead of losing it.
--
-- IDEMPOTENCE. The strictly-greater FlushSequence guard covers the delta columns exactly as it covers the
-- absolute ones: an exact replay of the same row -- the only replay the PositionWriteBehindHost retry queue
-- produces -- fails the guard and applies nothing. The residual window (SQL commit succeeded but the client
-- observed an exception, AND a later mutation then bumps FlushSequence) is strictly narrower than the one
-- already accepted by game.usp_Character_AdjustMoney, which is an unguarded X = X + @Delta.
--
-- WHY A NEW SCRIPT INSTEAD OF EDITING THE BASE FILES
-- Schemas/Types/game/tvp_CharacterProgress.sql, StoredProcedures/game/usp_Character_PersistProgressBatch.sql,
-- StoredProcedures/game/usp_Character_PersistFinalFlush.sql and
-- StoredProcedures/game/usp_Character_GetForWorldEntry.sql are all listed in _manifest.txt and SHA-256
-- journaled by Fenrir.Tools.DbMigrator, which refuses to re-apply a journaled path whose content changed. Same
-- reasoning and same shape as Migrations/002_character_progress_mount_writeback.sql: a fresh database applies
-- the base files then this one, a provisioned database applies only this one, both converge.
--
-- ORDERING WITHIN THIS WAVE -- LOAD-BEARING
-- Several concurrent write-behind extensions each DROP+CREATE the same type and the same three procedures, so
-- each script must carry the RUNNING UNION of every column added before it and the last one applied is the
-- authoritative shape. This script assumes Migrations/006_avatar_state_flags_writeback.sql ran first (it
-- carries VisibleState/SpecialState/UseOrnament, reproduced verbatim below, and it is the one that ALTERs
-- game.Characters to add UseOrnament) and it must itself run before
-- Migrations/007_costume_and_companion_timer_writeback.sql, which already carries this script's five columns
-- plus its own four. _manifest.txt order is what enforces that, not the file names.
--
-- WHY DROP+CREATE THE TYPE
-- A TABLE type cannot be ALTERed to add columns and cannot be dropped while a procedure takes it as a
-- parameter, so both write-behind procedures are dropped first and recreated after. EXECUTE permissions are
-- unaffected: Schemas/002_roles.sql grants EXECUTE at the SCHEMA level, which every future object inherits.

-- 1. Drop both procedures that take the type as a parameter, then the type.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 2. Recreate the type. Body identical to Migrations/006_avatar_state_flags_writeback.sql's version plus five
--    appended columns.
-- Title/Halo/TeacherPoint carry the absolute runtime value; WarPointDelta/BloodCoinDelta carry the points
-- EARNED since the last successful flush, never a balance -- see this file's header for why the two currencies
-- cannot use the absolute regime. Appended at the tail because CaeriusNet's generated TVP mapper binds
-- positionally (Fenrir.Data.Abstractions.Characters.CharacterProgressTvp), so column order is the contract.
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

-- 3. Recreate usp_Character_PersistProgressBatch with the five new SET clauses.
-- Idempotent: same strictly-greater FlushSequence guard as usp_Character_PersistBatch (one shared
-- monotonic sequence per character across both flush flavors).
-- Money/BigMoney intentionally absent: balances only move through usp_Character_AdjustMoney, to
-- avoid a last-write-wins flush clobbering a balance. WarPoint/BloodCoin are in the same category and are
-- therefore credited RELATIVELY here so this flush composes with usp_Character_BuyWarPointItem /
-- usp_Character_SpendBloodCoinAndReplaceContainer instead of racing them. Title/Halo/TeacherPoint have no
-- other writer and are absolute, same category as ContributionPoints.
-- The + on the two delta columns cannot violate CK_Characters_WarPoint / CK_Characters_BloodCoin (both
-- CHECK >= 0): the caller only ever accumulates grants there, so the delta is never negative.
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
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 4. Recreate usp_Character_PersistFinalFlush with the same five SET clauses.
-- Single-character, single-statement counterpart to usp_Character_PersistProgressBatch +
-- usp_Character_PersistBatch, used only by the disconnect-time and zone-change immediate flush
-- (PositionWriteBehindHost.FlushCharacterNowAsync). Same strictly-greater FlushSequence idempotence guard;
-- both TVPs carry one row each, stamped with the same FlushSequence value by the caller -- which is what makes
-- the retry queue's replay of an already-committed row a no-op on the two delta columns too.
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
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 5. Recreate usp_Character_GetForWorldEntry with c.BloodCoin appended to RS0.
-- Title/Halo/TeacherPoint/WarPoint were already selected here; BloodCoin was the only one of the five with no
-- read path at all, so PlayerRuntimeState.BloodCoin started every session at 0 regardless of the stored
-- balance -- persisting the grants without this would have been worse than not persisting them, because the
-- next flush's delta would have been computed against a phantom baseline. Appended at the tail after the
-- VisibleState/SpecialState/UseOrnament that Migrations/006_avatar_state_flags_writeback.sql added:
-- CaeriusNet's generated reader for CharacterWorldSnapshotDto binds by ordinal, so RS0 stays strictly
-- append-only and no existing ordinal moves.
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
