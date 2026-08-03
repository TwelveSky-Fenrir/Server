-- Concurrency hardening, same shape as usp_Character_Create_ConcurrentCreateGuard.sql: the shipped
-- usp_Character_CreateWithStarterKit_MountFix.sql only IF EXISTS-prechecks the slot/name collisions before
-- an unguarded INSERT into game.Characters. Under RCSI a plain SELECT takes no lock that would stop a
-- second concurrent create call for the same @AccountId/@Slot or @Name from also passing the precheck
-- before either INSERT commits -- the loser then hits a raw, uncataloged UQ_Characters_Account_Slot/
-- UQ_Characters_Name violation (2627, or 2601 if the engine reports it via the index) instead of the
-- documented 50201/50202 THROW. Body is otherwise byte-for-byte usp_Character_CreateWithStarterKit_MountFix.sql
-- (including its 1314/ANIMAL_NUM_BEAR2 starter mount grant, power 10) -- only the write shape changed: the
-- whole BEGIN TRANSACTION..COMMIT TRANSACTION block is now inside BEGIN TRY, with a CATCH that translates
-- ERROR_NUMBER() 2627/2601 into the correct one of 50201/50202 via a diagnostic re-check, and re-THROWs
-- anything else unchanged. XACT_ABORT ON already auto-rolls-back the explicit transaction the instant any
-- statement inside it fails (verified against Microsoft Learn), so no explicit ROLLBACK is needed in the
-- CATCH -- by the time it runs, @@TRANCOUNT is already back to 0 and the whole 5-statement starter-kit
-- write is already fully undone; the CATCH only has to pick the right error number to surface. CREATE OR
-- ALTER, additive: usp_Character_CreateWithStarterKit_MountFix.sql itself is not edited (SHA-256
-- journaling) -- see Database/_manifest.txt's own precedent for this shape
-- (usp_Character_CreateWithStarterKit_MountFix.sql superseding usp_Character_CreateWithStarterKit.sql the
-- same way).
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
        THROW 50201, N'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, N'Character name already taken.', 1;

    DECLARE @CharacterId TABLE
                         (
                             CharacterId INT
                         );
    DECLARE @NewCharacterId INT;

    BEGIN TRY
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
        VALUES (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX,
                @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 1, 0, 0, 0, 0, 50, 0,
                1314, 0, 10, 0, 99999999, 5, 1440, 300, 300, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate,
                @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

        SET @NewCharacterId = (SELECT CharacterId FROM @CharacterId);

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
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2627, 2601)
            THROW;

        IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
            THROW 50201, N'Character slot already occupied for this account.', 1;

        THROW 50202, N'Character name already taken.', 1;
    END CATCH;

    SELECT @NewCharacterId AS CharacterId;
END;
