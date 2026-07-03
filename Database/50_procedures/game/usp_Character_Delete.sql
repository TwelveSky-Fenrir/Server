-- database/50_procedures/game/usp_Character_Delete.sql
-- Contract: delete a character from an account's slot (CL_DELETE_AVATAR_SEND).
-- Params:
--   @AccountId INT     -- auth.Accounts.AccountId
--   @Slot      TINYINT -- 0..2 (MAX_USER_AVATAR_NUM)
-- Result set: none.
-- Idempotent: yes -- deleting an already-empty slot affects 0 rows and raises no error;
-- the caller cannot distinguish "was empty" from "just deleted" (by design).
-- Errors: none.
CREATE PROCEDURE game.usp_Character_Delete @AccountId INT,
    @Slot      TINYINT
AS
BEGIN
    SET
NOCOUNT ON; SET
XACT_ABORT ON;

DELETE
FROM game.Characters
WHERE AccountId = @AccountId
  AND Slot = @Slot;
END;
