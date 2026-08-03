CREATE PROCEDURE game.usp_TribeSubMaster_GetByTribe @TribeId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TribeId,
           SlotIndex,
           CharacterId
    FROM game.TribeSubMasters
    WHERE TribeId = @TribeId
    ORDER BY SlotIndex;
END;
