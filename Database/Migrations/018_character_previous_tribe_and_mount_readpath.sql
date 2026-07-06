-- Corrective follow-up to Migrations/015_starter_kit_elite_grant.sql/016_starter_kit_create_atomicity.sql
-- (neither edited in place -- DbMigrator journals every script by content hash and refuses a changed
-- re-apply). Closes two gaps a fresh legacy re-audit of op17 CL_CREATE_AVATAR_SEND2 and zone-entry found:
--
-- 1) game.Characters has never had a PreviousTribe column at all. Server/ts25zone/S04_MyWork02.cpp:880-901
--    proves the legacy engine treats Tribe (the playable faction, 0-3) and PreviousTribe (the Noble Dragon/
--    Royal Serpent/Grand Tiger starter-kit template that selected this character's equipment/skills/hotkeys,
--    0-2) as two genuinely independent persisted fields -- checked for self-consistency at every zone entry
--    (Tribe in {0,1,2} requires PreviousTribe == Tribe; Tribe == 3 requires PreviousTribe in {0,1,2}) -- and
--    Server/ts25login/S04_MyWork02.cpp:582-751 confirms creation itself never cross-checks the two fields
--    against each other, so a legacy character *can* legitimately end up with a mismatched pair. Fenrir's
--    creation path already receives PreviousTribe as its own request field (CreateAvatarService.CreateAvatarAsync's
--    previousTribe parameter, used today only to select the starter-kit template) but has nowhere to persist
--    it, so every character's "previous tribe" was being silently synthesized elsewhere at runtime as
--    identical to Tribe -- unable to represent the legacy fourth-faction case or the mismatch scenario at
--    all. This migration only adds the column and threads it through the write/read stored procedures --
--    the actual zone-entry self-consistency check and the runtime PreviousTribe wiring are deliberately left
--    to the gameplay-domain-engineer phase that consumes this column; nothing in Application/ is touched here.
--
-- 2) usp_Character_GetForWorldEntry projects none of the 5 Mount* columns 015 added to game.Characters, even
--    though usp_Character_CreateWithStarterKit has grants them (literal ANIMAL_NUM_TIGER1=1301, power 5,
--    slot 0, permanent expiry -- Server/ts25login/S04_MyWork02.cpp:1174-1179) since that migration. A
--    repo-wide search found these 5 columns read by no procedure, repository, or DTO anywhere -- the granted
--    mount is durably stored but completely inert. This migration extends the read path only (procedure +
--    DTO); wiring PlayerRuntimeState's mount/companion fields from it, and surfacing Animal* on the
--    create-avatar response itself, are separate gameplay-domain-engineer/wire-protocol-implementer tasks.
--
-- PreviousTribe's legal domain is 0-2 (never 3 -- Behavior C above never allows PreviousTribe itself to be
-- the fourth faction, only Tribe); DEFAULT 0 backfills every already-created row with the "Noble Dragon"
-- template rather than leaving it nullable, since no legacy-cited placeholder value exists for "unknown".
ALTER TABLE game.Characters
    ADD PreviousTribe TINYINT NOT NULL CONSTRAINT DF_Characters_PreviousTribe DEFAULT 0;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2);
GO

-- Identical body to 016's version (BEGIN TRANSACTION-wrapped atomicity fix), plus:
--   * @PreviousTribe TINYINT = 0 appended as the LAST parameter (never insert a new parameter in the middle
--     of a shipped procedure's signature -- see this repo's sql-server-2025-stored-procedure-conventions
--     skill) so every existing caller that doesn't yet pass it keeps compiling/binding correctly; the
--     literal 0 default only matters for a raw ad hoc EXEC, since CaeriusNet's builder always supplies a
--     value once Fenrir.Data's CreateWithStarterKitAsync is updated to pass one through.
--   * PreviousTribe added to game.Characters' INSERT column list (this is an INSERT's own column list, not
--     the procedure's parameter order, so it is placed next to Tribe for readability with no bearing on the
--     append-only parameter rule above).
--   * The stale "pet/cape rows travel in via @Equipment" comment (inherited from 015, restated verbatim by
--     016) is corrected: @Equipment/@Inventory/@Skills/@Hotkeys carry only what CreateAvatarService.
--     BuildEquipmentRows/BuildInventoryRows/BuildSkillRows/BuildHotkeyRows put in them -- which does include
--     the pet/cape rows (added directly onto the equipment list, see BuildEquipmentRows), but the point
--     stands that neither table row exists in world.StarterKitEquipment; both are 100% C# constants
--     (StarterCapeItemId/StarterPetItemId) injected before this call, never DB catalog rows.
CREATE OR ALTER PROCEDURE game.usp_Character_CreateWithStarterKit
    @AccountId INT,
    @Slot TINYINT,
    @Name NVARCHAR(13),
    @Tribe TINYINT,
    @Gender TINYINT,
    @HeadType TINYINT,
    @FaceType TINYINT,
    @MapId SMALLINT,
    @PosX REAL,
    @PosY REAL,
    @PosZ REAL,
    @Life INT,
    @MaxLife INT,
    @Mana INT,
    @MaxMana INT,
    @WelcomeBuffUntilDate INT,
    @PremiumUntilUnixSeconds BIGINT,
    @Equipment game.tvp_CharacterItemSlot READONLY,
    @Inventory game.tvp_CharacterItemSlot READONLY,
    @Skills game.tvp_CharacterSkillSlot READONLY,
    @Hotkeys game.tvp_CharacterHotkeySlot READONLY,
    @PreviousTribe TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, 'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, 'Character name already taken.', 1;

    DECLARE @CharacterId TABLE (CharacterId INT);

    BEGIN TRANSACTION;

    -- EU33 defaults: every stat starts at 1 (not the column default of 0) -- Server/ts25login/S04_MyWork02.cpp:
    -- 740-751, set unconditionally before the PreviousTribe/race switch, identical for every tribe/previous-
    -- tribe/gender. Every new character is also granted the same starter pet growth/activity (PetGrowth
    -- 640000000 = designer-facing "200%", PetActivity 100 = MAX_PAT_ACTIVITY_SIZE/full activity --
    -- Server/ts25login/S04_MyWork02.cpp:1132-1135, Server/Header/Protocol/DEFINE.h:612) and the same starting
    -- level/rebirth/experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179,
    -- forced on by Migrations/015's own USE_CUSTOME_CREATE citation) regardless of tribe -- none of these 9
    -- literal values vary by race/tribe/gender. The pet/cape item rows themselves travel in via @Equipment
    -- (added by CreateAvatarService.BuildEquipmentRows alongside the tribe's elite armor/gloves/boots/ring/
    -- amulet/weapon) -- neither is a world.StarterKitEquipment catalog row, both are C# constants.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
        OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES
        (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000, 1301, 0, 5, 0, 99999999, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

    DECLARE @NewCharacterId INT = (SELECT CharacterId FROM @CharacterId);

    INSERT INTO game.CharacterItems
    (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId,
           2,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Equipment;

    INSERT INTO game.CharacterItems
    (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId,
           0,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Inventory;

    INSERT INTO game.CharacterSkills (CharacterId, SlotIndex, SkillId, Grade)
    SELECT @NewCharacterId, SlotIndex, SkillId, Grade
    FROM @Skills;

    INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2)
    SELECT @NewCharacterId, Page, KeyIndex, Sort, Value1, Value2
    FROM @Hotkeys;

    COMMIT TRANSACTION;

    SELECT @NewCharacterId AS CharacterId;
END;
GO

-- Adds PreviousTribe and the 5 Mount* columns to RS0, both appended at the very end of the SELECT list
-- (after Exp2) -- never inserted mid-list -- so CharacterWorldEntryDto (the narrow, stable prefix used by
-- CreateAvatarService/ZoneTransferService, ordinally mapped onto this same result set) keeps reading the
-- exact same first 19 columns it always has and needs no change. Only CharacterWorldSnapshotDto (RS0's full
-- projection, used by GetWorldEntryBundleAsync/EnterWorldService) gains the 6 new trailing fields.
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
           c.MountTime
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           Quantity,
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
