-- All-4-tribes bank-total read in one round trip (game.Tribes has exactly 4 rows, TribeId 0-3 -- same
-- small, fixed cardinality as usp_Tribe_GetAll), via game.vw_TribeBankTotals (see that view's own header
-- for why it can never legally become an indexed view). LEFT JOIN + COALESCE so a tribe that has never
-- received a deposit (no rows in game.TribeBank at all) still comes back as TotalAmount = 0 /
-- OccupiedSlotCount = 0 rather than being missing from the result set entirely -- the same convention
-- game.usp_Guild_GetAll already applies over game.vw_GuildRosterCounts.
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
