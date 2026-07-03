-- Contract: @AccountId INT, @Success BIT -> no result set.
-- Idempotent when @Success = 1 (always resets to the same clean state, replay-safe). NOT idempotent
-- when @Success = 0: each call increments FailedLoginCount, so a retried/duplicated failure report
-- would over-count -- callers must report each real attempt exactly once.
-- Progressive lockout escalation (architecture reference §9.1), expressed as a single UPDATE/CASE so
-- the thresholds live in one place instead of duplicated IF/ELSE branches:
--   @Success = 1             -> FailedLoginCount = 0,          LockoutUntilUtc = NULL
--   @Success = 0, count >=10 -> FailedLoginCount = count + 1,  LockoutUntilUtc = now + 15 minutes
--   @Success = 0, count >= 5 -> FailedLoginCount = count + 1,  LockoutUntilUtc = now + 1 minute
--   @Success = 0, count < 5  -> FailedLoginCount = count + 1,  LockoutUntilUtc unchanged
CREATE PROCEDURE auth.usp_Account_RecordLoginAttempt @AccountId INT,
    @Success   BIT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE auth.Accounts
SET FailedLoginCount = CASE WHEN @Success = 1 THEN 0 ELSE FailedLoginCount + 1 END,
    LockoutUntilUtc  = CASE
                           WHEN @Success = 1 THEN NULL
                           WHEN FailedLoginCount + 1 >= 10 THEN DATEADD(MINUTE, 15, SYSUTCDATETIME())
                           WHEN FailedLoginCount + 1 >= 5 THEN DATEADD(MINUTE, 1, SYSUTCDATETIME())
                           ELSE LockoutUntilUtc
        END
WHERE AccountId = @AccountId;
END;
