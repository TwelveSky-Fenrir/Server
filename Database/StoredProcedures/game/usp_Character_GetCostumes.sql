CREATE PROCEDURE game.usp_Character_GetCostumes @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Slot,
           ItemId,
           ItemValue,
           ExpireDate
    FROM game.CharacterCostumeSlots
    WHERE CharacterId = @CharacterId
    ORDER BY Slot;
END;
