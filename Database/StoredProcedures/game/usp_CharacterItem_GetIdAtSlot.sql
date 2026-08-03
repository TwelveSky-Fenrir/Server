CREATE PROCEDURE game.usp_CharacterItem_GetIdAtSlot @CharacterId INT,
                                                    @Container TINYINT,
                                                    @Slot TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ItemId
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container
      AND Slot = @Slot;
END;
