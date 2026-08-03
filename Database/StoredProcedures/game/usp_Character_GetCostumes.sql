CREATE PROCEDURE game.usp_Character_GetCostumes @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Slot,
           ItemId,
           ItemDate,
           ExpireDate
    FROM game.CharacterCostumes
    WHERE CharacterId = @CharacterId
    ORDER BY Slot;
END;
