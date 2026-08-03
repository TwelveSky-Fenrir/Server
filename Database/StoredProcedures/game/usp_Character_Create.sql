CREATE PROCEDURE game.usp_Character_Create @AccountId INT,
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
                                           @MaxMana INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, N'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, N'Character name already taken.', 1;

    BEGIN TRY
        INSERT INTO game.Characters
        (AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType,
         MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana)
        OUTPUT INSERTED.CharacterId
        VALUES (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ,
                @Life, @MaxLife, @Mana, @MaxMana);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2627, 2601)
            THROW;

        IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
            THROW 50201, N'Character slot already occupied for this account.', 1;

        THROW 50202, N'Character name already taken.', 1;
    END CATCH;
END;
