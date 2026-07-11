-- op17 CL_CREATE_AVATAR_SEND2's full creation path: usp_Character_Create's slot/name guards plus the starter
-- kit in the same transaction. Left as a separate proc from usp_Character_Create rather than extending it in
-- place: that proc is still the minimal-dependency character factory every other feature's test suite uses to
-- satisfy game.Characters' FK, and giving every one of those call sites this many new required parameters
-- (plus 4 TVPs) would be unrelated churn.
--
-- CONFIRMED PRODUCT DECISION (character-creation-level1-redesign, NOT a legacy-parity fix): a freshly created
-- character starts at genuine Level 1 with only a basic weapon + torso/chest armor piece equipped, NOT the
-- EU33/USE_CUSTOME_CREATE instant-elite grant (Level 145, 6-slot Enchant45/Combine6 gear, starter mount/pet/
-- cape, one premium day, ProtectForDeath/AutoTime2/DoubleExpTime1-2 instant-boost counters) that the original
-- EU33-parity version of this procedure used -- see Tables/world/StarterKitEquipment.sql's own header for the
-- full USE_CUSTOME_CREATE citation this deliberately departs from.
--   * StatVit/StatStr/StatInt/StatDex = 1 (the Level-1 floor); PetGrowth/PetActivity = 0/0 (no pet is ever
--     granted by the redesigned equipment set, see CreateAvatarService.BuildEquipmentRows); Level/Level2/
--     RebirthCount/Experience/Exp2 = 1/0/0/0/0 (genuine Level 1, zero rebirths, no post-cap ladder progress).
--   * StatPoints/SkillPoints = 50/0: Fenrir product defaults, NOT legacy-cited (no compiled non-
--     USE_CUSTOME_CREATE branch exists anywhere in the reviewed Server/ts25login source to draw a level-1
--     starting pool from -- see CreateAvatarService.StartingStatPoint's own remarks). SkillPoints starts at 0
--     since the starter kit already grants every starting skill directly via game.CharacterSkills
--     (BuildSkillRows/world.StarterKitSkills).
--   * MountItemId/MountExpActivity/MountPower/MountSlotIndex/MountTime = 0/0/0/-1/0 (column DEFAULTs, no
--     starter mount); ProtectForDeath/AutoTime2 = 0/0 and DoubleExpTime1/DoubleExpTime2 = 0/0 (column
--     DEFAULTs, no "instant boost" grants).
--   * AutoBuffTime/InventoryDate/StoreDate stay @WelcomeBuffUntilDate (today + 7 days): the welcome-buff/
--     second-inventory-page/second-store-page rental grant is independent of the old EU33 instant-elite block
--     and survives the redesign.
--   * PremiumExpireUtc = 0 (column DEFAULT, "0 = none"): no premium-day grant. @PremiumUntilUnixSeconds stays a
--     declared parameter (signature parity with every existing caller of
--     CharacterRepository.CreateWithStarterKitAsync) but is DELIBERATELY UNUSED in the INSERT -- CreateAvatarService.cs
--     passes a fixed 0 for it.
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
            @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 50, 0,
            0, 0, 0, -1, 0, 0, 0, 0, 0, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate,
            @WelcomeBuffUntilDate, 0);

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
