-- database/50_procedures/game/usp_Character_Rename.sql
-- Contract: rename the character occupying (@AccountId, @Slot) -- CL_CHANGE_AVATAR_NAME_SEND (op 19).
-- Params:
--   @AccountId INT          -- auth.Accounts.AccountId
--   @Slot      TINYINT      -- 0..2 (MAX_USER_AVATAR_NUM)
--   @NewName   NVARCHAR(13) -- MAX_AVATAR_NAME_LENGTH, must be globally unique
-- Result set (RS0, single row/column -- ExecuteScalarAsync<int>), the LEGACY client-visible codes of
-- mDB.ChangeCharacterName (login protocol report §4.19) so the handler forwards them verbatim:
--   0   -- renamed
--   2   -- @NewName already taken (this also covers "new name == current name": the row itself matches)
--   102 -- no character at (@AccountId, @Slot) (legacy "update failure" -- slot emptied concurrently)
-- (101 -- "SQL error" -- is not selected here: any real engine error surfaces as an exception, which the
--  handler maps to 101, same shape as the legacy catch-all.)
-- Scalar codes instead of THROW (the usp_Character_Create style) because these values ARE the wire protocol:
-- the legacy client renders each one, so round-tripping them as data keeps the mapping in exactly one place.
-- Idempotent: no for a successful rename; retrying a success returns 2 (name now taken by the row itself).
CREATE PROCEDURE game.usp_Character_Rename @AccountId INT,
    @Slot    TINYINT,
    @NewName NVARCHAR(13)
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM game.Characters WHERE Name = @NewName)
BEGIN
SELECT 2;
RETURN;
END;

UPDATE game.Characters
SET Name = @NewName
WHERE AccountId = @AccountId
  AND Slot = @Slot;

IF
@@ROWCOUNT = 0
BEGIN
SELECT 102;
RETURN;
END;

SELECT 0;
END;
