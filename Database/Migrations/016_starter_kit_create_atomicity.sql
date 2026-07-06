-- Corrective follow-up to Migrations/015_starter_kit_elite_grant.sql (never edited in place -- DbMigrator
-- journals every script by content hash and refuses a changed re-apply).
--
-- Bug: neither the original game.usp_Character_CreateWithStarterKit.sql nor 015's CREATE OR ALTER
-- replacement wraps its 5 write statements (INSERT game.Characters, INSERT game.CharacterItems x2 for
-- @Equipment/@Inventory, INSERT game.CharacterSkills, INSERT game.CharacterHotkeys) in an explicit
-- BEGIN TRANSACTION / COMMIT TRANSACTION. SET XACT_ABORT ON does not by itself make a multi-statement batch
-- atomic: with no explicit transaction, SQL Server runs in autocommit mode, where every individual statement
-- is its own transaction that commits as soon as it completes successfully. XACT_ABORT ON only guarantees
-- that a run-time error rolls back *the current transaction* -- in autocommit mode that is just the one
-- failing statement, not any statement that already committed earlier in the same batch. Concretely: if the
-- INSERT INTO game.Characters succeeds and one of the four subsequent child inserts then fails (a bad
-- @Equipment/@Inventory row violating FK_CharacterItems_Item, a duplicate slot violating PK_CharacterItems,
-- an @Skills/@Hotkeys row violating its own PK/FK), the new character row is left committed with a partial
-- or entirely missing starter kit -- exactly the atomicity failure this repo's stored-procedure conventions
-- exist to prevent (see e.g. usp_CharacterItems_ReplaceTwoContainers.sql's own header, or
-- usp_Character_Delete_CleanupChildTables.sql/usp_CharacterQuest_ApplyTransition.sql, both of which already
-- wrap their multi-statement bodies in BEGIN TRANSACTION/COMMIT TRANSACTION).
--
-- Fix: identical body to 015's version (same parameters, same literal EU33/USE_CUSTOME_CREATE grants), just
-- with the five write statements wrapped in one explicit transaction. The two pre-checks (slot/name already
-- taken) stay outside the transaction since they are read-only and the table's own UNIQUE constraints are
-- still the concurrent-race backstop, exactly as before.
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
    @Hotkeys game.tvp_CharacterHotkeySlot READONLY
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

    -- EU33 defaults: every stat starts at 1 (not the column default of 0); every new character is granted the
    -- same starter pet growth/activity (200% growth, full activity) and the same starting level/rebirth/
    -- experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179, forced on by
    -- Migrations/015's own USE_CUSTOME_CREATE citation) regardless of tribe. The pet/cape item rows themselves
    -- travel in via @Equipment alongside the tribe's elite armor/gloves/boots/ring/amulet/weapon.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
        OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES
        (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000, 1301, 0, 5, 0, 99999999, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

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
