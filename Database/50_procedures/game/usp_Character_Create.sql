-- database/50_procedures/game/usp_Character_Create.sql
-- Contract: create a new character in a free slot (CL_CREATE_AVATAR_SEND2).
-- Params:
--   @AccountId INT          -- auth.Accounts.AccountId
--   @Slot      TINYINT      -- 0..2 (MAX_USER_AVATAR_NUM), must be free for this account
--   @Name      NVARCHAR(13) -- MAX_AVATAR_NAME_LENGTH, must be globally unique
--   @Tribe     TINYINT
--   @Gender    TINYINT
--   @HeadType  TINYINT
--   @FaceType  TINYINT
--   @MapId     SMALLINT     -- spawn map
--   @PosX      REAL
--   @PosY      REAL
--   @PosZ      REAL
--   @Life      INT
--   @MaxLife   INT
--   @Mana      INT
--   @MaxMana   INT
-- Result set (RS0, single row/column -- ExecuteScalarAsync<int> or FirstQueryAsync<T>):
--   1. CharacterId INT (identity of the row just inserted)
-- Idempotent: no (each call that succeeds creates one new character).
-- Errors:
--   THROW 50201 -- @Slot already occupied for @AccountId (checked before the insert;
--                   UQ_Characters_Account_Slot is the last-resort backstop under a race).
--   THROW 50202 -- @Name already taken (checked before the insert; UQ_Characters_Name is
--                   the last-resort backstop under a race).
CREATE PROCEDURE game.usp_Character_Create @AccountId INT,
    @Slot      TINYINT,
    @Name      NVARCHAR(13),
    @Tribe     TINYINT,
    @Gender    TINYINT,
    @HeadType  TINYINT,
    @FaceType  TINYINT,
    @MapId     SMALLINT,
    @PosX      REAL,
    @PosY      REAL,
    @PosZ      REAL,
    @Life      INT,
    @MaxLife   INT,
    @Mana      INT,
    @MaxMana   INT
AS
BEGIN
    SET
NOCOUNT ON; SET
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
VALUES
    (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana);
END;
