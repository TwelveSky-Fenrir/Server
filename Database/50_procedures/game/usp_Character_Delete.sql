-- Idempotent: deleting an already-empty slot affects 0 rows and raises no error (by design).
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
