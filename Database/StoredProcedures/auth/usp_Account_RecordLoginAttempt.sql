-- NOT idempotent when @Success = 0: each call increments FailedLoginCount, so callers must report
-- each real attempt exactly once. Progressive lockout: >=10 failures -> 15 min, >=5 -> 1 min.
CREATE PROCEDURE auth.usp_Account_RecordLoginAttempt @AccountId INT,
                                                     @Success BIT
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
