-- database/50_procedures/game/usp_Gift_Enqueue.sql
-- Contract: create a new pending gift for an account (e.g. a cash-shop purchase or GM delivery).
-- Params:
--   @AccountId INT      -- auth.Accounts.AccountId
--   @ProductId INT NULL -- see game.Gifts for why this is an unenforced reference, not a real FK; NULL for
--                           "no product reference" (translate legacy 0 -> NULL before calling)
--   @Quantity  INT
--   @Value     INT
-- Result set (RS0, single row/column -- ExecuteScalarAsync<int>): 1. GiftId INT (identity of the new row).
-- Idempotent: no (each call creates one new gift). Also appends a GiftLog row capturing this Pending state,
-- so the log has an entry for every state a gift ever passed through, not just its terminal claim.
-- Errors: none.
CREATE PROCEDURE game.usp_Gift_Enqueue @AccountId INT,
    @ProductId INT = NULL,
    @Quantity  INT,
    @Value     INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE
@Inserted TABLE (GiftId INT);

INSERT INTO game.Gifts (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
    OUTPUT INSERTED.GiftId INTO @Inserted (GiftId)
VALUES (@AccountId, @ProductId, @Quantity, @Value, 0, @Now);

INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
VALUES (@AccountId, @ProductId, @Quantity, @Value, 0, @Now);

SELECT GiftId
FROM @Inserted;
END;
