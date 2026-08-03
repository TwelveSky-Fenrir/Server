CREATE PROCEDURE game.usp_TribeBank_GetTotals
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT t.TribeId,
           COALESCE(b.TotalAmount, CAST(0 AS BIGINT)) AS TotalAmount,
           COALESCE(b.OccupiedSlotCount, 0)           AS OccupiedSlotCount
    FROM game.Tribes t
             LEFT JOIN game.vw_TribeBankTotals b ON b.TribeId = t.TribeId
    ORDER BY t.TribeId;
END;
