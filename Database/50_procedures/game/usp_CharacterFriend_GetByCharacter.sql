-- database/50_procedures/game/usp_CharacterFriend_GetByCharacter.sql
-- Contract: this character's OWN 10-slot friend list (legacy aFriend[MAX_FRIEND_NUM]), resolved to the
-- friend's live name too (a friend can be renamed since the slot was filled -- the wire always shows the
-- CURRENT name, matching CZ_FRIEND_FIND_SEND's own live-lookup semantics).
-- Params:
--   @CharacterId INT
-- Result set (RS0), ordered by Slot, 0..10 rows:
--   1. Slot              TINYINT (0-9)
--   2. FriendCharacterId  INT
--   3. FriendName         NVARCHAR(13)
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_CharacterFriend_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT f.Slot, f.FriendCharacterId, FriendName = c.Name
FROM game.CharacterFriends AS f
         JOIN game.Characters AS c ON c.CharacterId = f.FriendCharacterId
WHERE f.CharacterId = @CharacterId
ORDER BY f.Slot;
END;
