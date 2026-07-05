-- Idempotent: deleting an already-empty slot is a silent no-op (the legacy quits on this case; not reproduced here).
CREATE PROCEDURE game.usp_CharacterFriend_Remove @CharacterId INT,
    @Slot        TINYINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.CharacterFriends
WHERE CharacterId = @CharacterId
  AND Slot = @Slot;
END;
