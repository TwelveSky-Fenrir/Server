CREATE PROCEDURE game.usp_Character_GetMounts @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Slot,
           ItemId,
           ExpActivity,
           Power
    FROM game.CharacterMounts
    WHERE CharacterId = @CharacterId
    ORDER BY Slot;
END;
