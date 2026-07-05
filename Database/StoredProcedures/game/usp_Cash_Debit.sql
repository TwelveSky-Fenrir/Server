-- Reactivates an overdraft guard that shipped commented-out in the legacy cash-shop v2 (purchase
-- without debit). A missing AccountCash row (never credited) is treated as insufficient funds.
CREATE PROCEDURE game.usp_Cash_Debit @AccountId INT,
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
@Debited TABLE (BalanceAfter INT);

BEGIN
TRANSACTION;

UPDATE game.AccountCash
SET Balance      = Balance - @Amount,
    UpdatedAtUtc = SYSUTCDATETIME() OUTPUT INSERTED.Balance
INTO @Debited
WHERE AccountId = @AccountId
  AND Balance >= @Amount; -- overdraft guard

IF
@@ROWCOUNT = 0
        THROW 50240, N'Insufficient cash balance for this debit.', 1;

INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
FROM @Debited;

COMMIT TRANSACTION;
END;
