-- database/50_procedures/game/usp_Cash_Credit.sql
-- Contract: atomically credit real-money cash (top-up/GM grant/refund -- the write half legacy spread
-- across login/playuser/extra's uCash writers, now a single writer, report 06 §3.10), writing the
-- game.CashLog audit row IN THE SAME TRANSACTION.
-- Params:
--   @AccountId INT
--   @Amount    INT        -- must be positive; the amount to add
--   @Reason    TINYINT    -- app-defined CashLog category (e.g. top-up, GM grant, refund)
--   @ProductId INT = NULL -- optional reference for the audit row (e.g. a refunded product)
-- Result set: none.
-- Idempotent: no (each successful call credits @Amount).
-- First credit for an account mints its game.AccountCash row (UPDATE-then-INSERT-if-missing; a race of
-- two first-credits is backstopped by PK_AccountCash -- one INSERT loses and the whole transaction
-- retries at the caller, which is exactly right for money).
-- Errors:
--   THROW 50241 -- @Amount is not positive (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Cash_Credit @AccountId INT,
    @Amount    INT,
    @Reason    TINYINT,
    @ProductId INT = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
@Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    DECLARE
@Credited TABLE (BalanceAfter INT);

BEGIN
TRANSACTION;

UPDATE game.AccountCash
SET Balance      = Balance + @Amount,
    UpdatedAtUtc = SYSUTCDATETIME() OUTPUT INSERTED.Balance
INTO @Credited
WHERE AccountId = @AccountId;

IF
@@ROWCOUNT = 0
BEGIN
INSERT INTO game.AccountCash (AccountId, Balance)
VALUES (@AccountId, @Amount);

INSERT INTO @Credited (BalanceAfter)
VALUES (@Amount);
END;

INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
FROM @Credited;

COMMIT TRANSACTION;
END;
