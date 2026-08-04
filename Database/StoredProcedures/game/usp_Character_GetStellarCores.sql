CREATE PROCEDURE game.usp_Character_GetStellarCores @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Slot,
           ItemId
    FROM game.CharacterStellarCoreSlots
    WHERE CharacterId = @CharacterId
    ORDER BY Slot;
END;
