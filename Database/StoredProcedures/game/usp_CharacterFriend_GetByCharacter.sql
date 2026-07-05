-- FriendName is resolved live via join (a renamed friend shows their current name, not a stored snapshot).
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
