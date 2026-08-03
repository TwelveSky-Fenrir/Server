CREATE VIEW game.vw_TribeBankTotals
AS
SELECT TribeId,
       SUM(CAST(Amount AS BIGINT)) AS TotalAmount,
       COUNT(*)                    AS OccupiedSlotCount
FROM game.TribeBank WITH (SNAPSHOT)
GROUP BY TribeId;
