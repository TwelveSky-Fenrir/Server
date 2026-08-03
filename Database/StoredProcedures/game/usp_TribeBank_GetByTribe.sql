CREATE PROCEDURE game.usp_TribeBank_GetByTribe @TribeId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TribeId,
           SlotIndex,
           Amount
    FROM game.TribeBank
    WHERE TribeId = @TribeId
    ORDER BY SlotIndex;
END;
