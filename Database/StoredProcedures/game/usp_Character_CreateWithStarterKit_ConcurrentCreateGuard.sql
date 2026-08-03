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
