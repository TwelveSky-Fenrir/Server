-- Starting death-protection allowance grant (aProtectForDeath): Server/ts25login/S04_MyWork02.cpp:885-889
-- (the live #ifdef LNW33 block, compiled in the shipped ReleaseEU33 build, in the same block as the
-- DoubleExpTime1/DoubleExpTime2/AutoTime2 literals already granted by
-- Migrations/018_character_previous_tribe_and_mount_readpath.sql) sets `tAvatarInfo.aProtectForDeath = 5;`
-- unconditionally for every newly created character, regardless of tribe/previous-tribe/gender.
-- Server/ts25zone/S07_MyGame02.cpp:3443-3489 confirms this is a decrementing starter/newbie death-protection
-- allowance consumed on avatar death once the defender is level >= 10 (ProcessAttack04, "Monster -> Avatar"):
-- when the counter is still > 0 it is decremented and no XP/contribution-point penalty applies; once it hits
-- 0 the normal death penalty (contribution-point loss at the level cap, or full general-XP loss below it)
-- applies instead. ServerDocs/12_ts25zone/10_MyGame02_Combat_ProcessAttack.md:315-317 corroborates the same
-- mechanic in the reverse-engineering notes.
--
-- game.Characters.ProtectForDeath already exists (Database/Tables/game/Characters.sql:55, NOT NULL DEFAULT 0,
-- CHECK >= 0) and the read path already projects it (usp_Character_GetForWorldEntry.sql:49's
-- c.ProtectForDeath, into CharacterWorldSnapshotDto.ProtectForDeath) -- but usp_Character_CreateWithStarterKit's
-- INSERT column list has never included it, in either the original (015/016) or the current (018) body, so
-- every newly created character silently takes the column's DEFAULT of 0 instead of the legacy-granted 5.
--
-- Fix-forward only (not edited in place -- DbMigrator journals every script by content hash and refuses a
-- changed re-apply): identical body to 018's version of usp_Character_CreateWithStarterKit, with ProtectForDeath
-- added to the INSERT column list and the literal 5 added to VALUES, next to the other unconditional starter
-- literals (StatVit/Str/Int/Dex, PetGrowth/PetActivity, Level/Level2/RebirthCount/Experience/Exp2,
-- StatPoints/SkillPoints, Mount*). No new stored-procedure parameter is introduced: per the cited range the
-- granted value is a fixed literal, not derived from any request field, so it does not vary by tribe,
-- previous tribe, or gender -- exactly like the other literals already hardcoded in this VALUES clause.
--
-- Wire-protocol side deliberately out of scope for this migration: whether ProtectForDeath is additionally
-- echoed back to the client within AVATAR_INFO at creation/zone-entry time is not established by the cited
-- legacy range (AvatarInfoFactory.CreateForCharacter/CreateForRuntimeState currently leave AvatarInfo.
-- ProtectForDeath at its wire-zero default for every avatar projection, not just this one) -- that requires
-- its own legacy-behavior-translator contract before touching Application/Fenrir.Application.*.Domain.Avatars.
-- This migration only closes the persistence gap the audit found: the column now durably holds the correct
-- starting value of 5 so any current or future reader (including the already-existing GetForWorldEntry read
-- path and any future death-consumption logic) sees the legacy-accurate starting grant.
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
    -- Server/ts25login/S04_MyWork02.cpp:1132-1135, Server/Header/Protocol/DEFINE.h:612), the same starting
    -- level/rebirth/experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179,
    -- forced on by Migrations/015's own USE_CUSTOME_CREATE citation), and the same starting death-protection
    -- allowance (ProtectForDeath = 5, Server/ts25login/S04_MyWork02.cpp:885-889, this migration's own
    -- citation) regardless of tribe -- none of these 10 literal values vary by race/tribe/gender. The pet/cape
    -- item rows themselves travel in via @Equipment (added by CreateAvatarService.BuildEquipmentRows alongside
    -- the tribe's elite armor/gloves/boots/ring/amulet/weapon) -- neither is a world.StarterKitEquipment
    -- catalog row, both are C# constants.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     ProtectForDeath,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
        OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES
        (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000, 1301, 0, 5, 0, 99999999, 5, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

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
