-- database/50_procedures/game/usp_CharacterFriend_Remove.sql
-- Contract: CZ_FRIEND_DELETE_SEND (opcode 58) -- clears one friend slot for the calling character.
-- Params:
--   @CharacterId INT
--   @Slot        TINYINT (0-9)
-- Result set: none.
-- Idempotent: yes -- deleting an already-empty slot is a silent no-op (same posture as
-- usp_Character_Delete/admin.usp_Mute_Lift), matching the wire contract's own note that the legacy
-- Quits on an ALREADY-empty slot (a stricter anti-fuzzing posture this proc does not need to reproduce;
-- FriendRegistry, the sole caller, has already verified the slot is occupied before calling this).
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
