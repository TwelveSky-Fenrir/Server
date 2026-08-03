-- Concurrency hardening: the shipped usp_Character_Create.sql only IF EXISTS-precheck the slot/name
-- collisions before an unguarded INSERT. Under RCSI a plain SELECT takes no lock that would stop a second
-- concurrent Create call for the same @AccountId/@Slot or @Name from also passing the precheck before
-- either INSERT commits (classic check-then-act TOCTOU) -- the loser then hits a raw, uncataloged
-- UQ_Characters_Account_Slot/UQ_Characters_Name violation (2627, or 2601 if the engine reports it via the
-- index) instead of the documented 50201/50202 THROW. auth.usp_Account_Create already carries the fix for
-- the identical shape (TRY/CATCH around the INSERT translating 2627/2601 into the same domain error the
-- precheck would have thrown) -- applied here verbatim, distinguishing which of the two unique constraints
-- was hit so the caller still gets the correct one of 50201/50202 even when both callers' prechecks raced
-- past each other. CREATE OR ALTER, additive: usp_Character_Create.sql itself is not edited (SHA-256
-- journaling) -- see Database/_manifest.txt's own precedent for this shape
-- (usp_Character_CreateWithStarterKit_MountFix.sql, usp_Cash_Credit_UpperBoundGuard.sql).
CREATE OR ALTER PROCEDURE game.usp_Character_Create @AccountId INT,
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
