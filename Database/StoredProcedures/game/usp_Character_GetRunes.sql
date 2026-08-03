CREATE PROCEDURE game.usp_Character_GetRunes @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT SocketIndex,
           RuneItemId,
           RuneStat
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId
    ORDER BY SocketIndex;
END;
