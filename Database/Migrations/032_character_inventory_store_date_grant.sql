-- starter-kit-inventory-store-date-grant (Minor, creation-persistence-full-reaudit area):
-- usp_Character_CreateWithStarterKit's INSERT column list never included InventoryDate/StoreDate, so every
-- new character silently took the columns' DEFAULT of 0 instead of the legacy 7-day-future second-page
-- rental grant.
--
-- Server/ts25login/S04_MyWork02.cpp:863-872 -- the live (non-FREE_DATE, LNW33) branch:
--     tAvatarInfo.aInventoryDate = ReturnAddDate(0, 7);
--     tAvatarInfo.aStoreDate     = ReturnAddDate(0, 7);
-- runs unconditionally at creation. FREE_DATE is confirmed never #define'd anywhere in Server/ (repo-wide
-- grep, zero #define occurrences -- only the guard lines themselves), so its sibling branch (which would
-- instead set InventoryDate/StoreDate plus a pet-bag date and a "Zone126Time" field to a maximal sentinel)
-- is dead code in every configuration and is deliberately NOT reproduced here. LNW33 is confirmed active for
-- the shipped ReleaseEU33 configuration (Server/ts25latest_config.props:11 predefines LNW33EU;M33;
-- Server/Header/Protocol/DEFINE.h:21-23's `#ifdef M33` unconditionally #defines LNW33), so the 7-day grant
-- -- not the sibling non-LNW33 30-day/pet-bag-date branch -- is what actually ships.
--
-- Same fixed 7-day offset, computed the same way, as the adjacent aAutoBuffTime assignment three lines below
-- in the same conditional block (S04_MyWork02.cpp:891-892: "Starting Auto Buff Scroll: 7 days"). Fenrir
-- already computes exactly that value in CreateAvatarService.TodayPlusDays(WelcomeBuffDurationDays) and
-- passes it down as the existing @WelcomeBuffUntilDate parameter (persisted onto AutoBuffTime by 026/027).
-- This fix reuses that same parameter for InventoryDate/StoreDate rather than adding a new one: legacy
-- computes all three as "the moment of creation plus 7 days" with no per-tribe/gender/request variation, so
-- there is nothing distinct to parameterize separately -- adding a second identical parameter would only be
-- a duplicate of the first. No C# repository/service signature change is required: usp_Character_
-- GetForWorldEntry already selects InventoryDate/StoreDate back (027's own body, itself carried over from
-- 018), and both CharacterWorldSnapshotDto (Infrastructure/Fenrir.Data.Abstractions/Characters/
-- CharacterDtos.cs:105-106) and Game's AvatarInfoFactory.CreateForCharacter
-- (Application/Fenrir.Application.Game.Domain/Avatars/AvatarInfoFactory.cs:78-79) already carry both fields
-- through unchanged onto the outward avatar-info projection -- the only broken link was this procedure's own
-- INSERT column list.
--
-- Layered on top of 027's body (the newest version actually in effect after full migration replay, not
-- 026's -- the version the original finding cited) -- preserves ProtectForDeath/AutoTime2/DoubleExpTime1/
-- DoubleExpTime2/AutoBuffTime/PreviousTribe exactly as 025/026/027 left them.
--
-- Deliberately NOT in scope here (per this fix's own behavior contract, "Not observed in the cited range"
-- and "Not a new discovery"): the runtime comparison logic for what "the second inventory/store page is
-- currently unlocked" means, and how these two stored dates would be compared against a current date at the
-- moment of a page-2 access attempt, is untouched -- it remains exactly as already tracked in
-- Application/Fenrir.Application.Game.Domain/Inventory/StoreItemTransferPolicy.cs,
-- Application/Fenrir.Application.Game.Domain/Inventory/SaveBankItemTransferPolicy.cs,
-- Application/Fenrir.Application.Game.Handlers/Handlers/GenericActionHandler.cs,
-- Application/Fenrir.Application.Game.Handlers/Handlers/UseInventoryItemHandler.cs, and
-- Application/Fenrir.Application.Game.Domain/Crafting/RuneStoneCraftResolver.cs: no PlayerRuntimeState-
-- equivalent field exists yet for either date, so nothing downstream reads or compares these values today.
-- This migration only fixes the creation-time write half of that already-tracked gap, not the runtime-
-- consumer half.
CREATE OR ALTER PROCEDURE game.usp_Character_CreateWithStarterKit @AccountId INT,
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

    DECLARE @CharacterId TABLE
                         (
                             CharacterId INT
                         );

    BEGIN TRANSACTION;

    -- EU33 defaults: every stat starts at 1 (not the column default of 0) -- Server/ts25login/S04_MyWork02.cpp:
    -- 740-751, set unconditionally before the PreviousTribe/race switch, identical for every tribe/previous-
    -- tribe/gender. Every new character is also granted the same starter pet growth/activity (PetGrowth
    -- 640000000 = designer-facing "200%", PetActivity 100 = MAX_PAT_ACTIVITY_SIZE/full activity --
    -- Server/ts25login/S04_MyWork02.cpp:1132-1135, Server/Header/Protocol/DEFINE.h:612), the same starting
    -- level/rebirth/experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179,
    -- forced on by Migrations/015's own USE_CUSTOME_CREATE citation), the same starting death-protection
    -- allowance (ProtectForDeath = 5, Server/ts25login/S04_MyWork02.cpp:885-889, Migrations/025's own
    -- citation), the same welcome-buff counters (DoubleExpTime1/DoubleExpTime2 = bare literal 300,
    -- Server/ts25login/S04_MyWork02.cpp:886-887, Migrations/026's own citation), the same starting free
    -- auto-hunt minute allowance (AutoTime2 = bare literal 1440 = 24h worth of minutes,
    -- Server/ts25login/S04_MyWork02.cpp:888, Migrations/027's own citation), and the same starting second-
    -- inventory-page/second-store-page rental grant (InventoryDate/StoreDate = the same 7-days-from-now date
    -- as AutoBuffTime, Server/ts25login/S04_MyWork02.cpp:863-872, this migration's own citation) regardless of
    -- tribe -- none of these literal values vary by race/tribe/gender. The pet/cape item rows themselves
    -- travel in via @Equipment (added by CreateAvatarService.BuildEquipmentRows alongside the tribe's elite
    -- armor/gloves/boots/ring/amulet/weapon) -- neither is a world.StarterKitEquipment catalog row, both are
    -- C# constants.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     ProtectForDeath, AutoTime2,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, InventoryDate, StoreDate, PremiumExpireUtc)
    OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY,
            @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000,
            1301, 0, 5, 0, 99999999, 5, 1440, 300, 300, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate,
            @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

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
