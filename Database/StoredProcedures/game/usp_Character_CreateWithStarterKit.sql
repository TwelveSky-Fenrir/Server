-- op17 CL_CREATE_AVATAR_SEND2's full creation path: usp_Character_Create's slot/name guards plus the EU33
-- starter kit in the same transaction. Left as a separate proc from usp_Character_Create rather than
-- extending it in place: that proc is still the minimal-dependency character factory every other feature's
-- test suite uses to satisfy game.Characters' FK, and giving every one of those call sites this many new
-- required parameters (plus 4 TVPs) would be unrelated churn.
--
-- EU33/USE_CUSTOME_CREATE creation grant (Server/ts25login/S04_MyWork02.cpp:740-1179, forced on unconditionally
-- in every build configuration -- see Tables/world/StarterKitEquipment.sql's own header for the full
-- USE_CUSTOME_CREATE citation): every stat starts at 1 (not the column default of 0); every new character is
-- granted the same starter pet growth/activity (200%/full activity), starting level/rebirth/experience/
-- stat-skill-point/mount grant, starting death-protection allowance (ProtectForDeath=5), welcome-buff counters
-- (DoubleExpTime1/DoubleExpTime2=300, raw decrementing counters, NOT dates), starting free auto-hunt minute
-- allowance (AutoTime2=1440, 24h), and starting second-inventory-page/second-store-page rental grant
-- (InventoryDate/StoreDate = 7 days from now, same value as AutoBuffTime) -- regardless of tribe/gender. The
-- pet/cape item rows themselves travel in via @Equipment (added by CreateAvatarService.BuildEquipmentRows
-- alongside the tribe's elite armor/gloves/boots/ring/amulet/weapon) -- neither is a world.StarterKitEquipment
-- catalog row, both are C# constants.
--
-- Errors: 50201 slot already occupied, 50202 name already taken -- both pre-checked; the table's unique
-- constraints are the race backstop. All 5 write statements (Characters/CharacterItems x2/CharacterSkills/
-- CharacterHotkeys) run inside one explicit transaction: SET XACT_ABORT ON alone does not make a
-- multi-statement batch atomic in autocommit mode, so a mid-sequence failure (e.g. a bad @Equipment/
-- @Inventory row) must not leave a committed character row with a partial or missing starter kit.
CREATE PROCEDURE game.usp_Character_CreateWithStarterKit @AccountId INT,
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
