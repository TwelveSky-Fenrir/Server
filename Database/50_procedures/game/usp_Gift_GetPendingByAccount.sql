-- database/50_procedures/game/usp_Gift_GetPendingByAccount.sql
-- Contract: @AccountId -> RS0 { GiftId, ProductId, Quantity, Value, CreatedAtUtc }, one row per pending
--           (Status=0) gift for that account, ordered oldest-first (delivery order).
-- Read-only, safe to retry. Served by IX_Gifts_Account_Status (AccountId, Status).
CREATE PROCEDURE game.usp_Gift_GetPendingByAccount @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT GiftId, ProductId, Quantity, Value, CreatedAtUtc
FROM game.Gifts
WHERE AccountId = @AccountId
  AND Status = 0
ORDER BY CreatedAtUtc;
END;
