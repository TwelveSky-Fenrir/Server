-- Errors: 50201 slot already occupied, 50202 name already taken -- both pre-checked; the
-- table's unique constraints (UQ_Characters_Account_Slot/UQ_Characters_Name) are the race backstop.
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
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, 'Character slot already occupied for this account.', 1;

    IF
        EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, 'Character name already taken.', 1;

    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana)
    OUTPUT INSERTED.CharacterId
    VALUES (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life,
            @MaxLife, @Mana, @MaxMana);
END;
