-- database/50_procedures/game/usp_Cash_Debit.sql
-- Contract: atomically debit real-money cash with an overdraft guard, writing the game.CashLog audit
-- row IN THE SAME TRANSACTION (report 06 §3.2/§4.3). This is THE single debit path (D9): the legacy
-- cash-shop v2 shipped with this exact guard COMMENTED OUT (purchase without debit, report 06 §3.3) --
-- reactivated here and structurally unavoidable (procs-only + CK_AccountCash_Balance backstop).
-- Params:
--   @AccountId INT
--   @Amount    INT          -- must be positive; the amount to subtract
--   @Reason    TINYINT      -- app-defined CashLog category (e.g. item-mall purchase)
--   @ProductId INT = NULL   -- optional world.ItemMallProducts reference for the audit row
-- Result set: none.
-- Idempotent: no (each successful call debits @Amount; the caller retries only on a THROW, never on
-- success-in-doubt -- same regime-(b) contract as the other value-moving procs).
-- Single guarded UPDATE ... OUTPUT (usp_Gift_Claim's idiom) captures the post-debit balance for the log
-- row without a read/write race window; a missing AccountCash row (account never credited) is treated
-- as insufficient funds. THROW under an open transaction + XACT_ABORT rolls everything back.
-- Errors:
--   THROW 50241 -- @Amount is not positive (admin.ErrorCatalog, 502xx = game range).
--   THROW 50240 -- insufficient cash balance (or account never credited).
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
  AND Balance >= @Amount; -- the reactivated overdraft guard (D9)

IF
@@ROWCOUNT = 0
        THROW 50240, N'Insufficient cash balance for this debit.', 1;

INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
FROM @Debited;

COMMIT TRANSACTION;
END;
