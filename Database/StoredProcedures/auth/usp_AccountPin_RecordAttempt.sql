CREATE PROCEDURE auth.usp_AccountPin_RecordAttempt @AccountId INT,
                                                   @Success BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE auth.AccountPins
    SET FailedLoginCount = CASE WHEN @Success = 1 THEN 0 ELSE FailedLoginCount + 1 END,
        LockoutUntilUtc  = CASE
                               WHEN @Success = 1 THEN NULL
                               WHEN FailedLoginCount + 1 >= 10 THEN DATEADD(MINUTE, 15, SYSUTCDATETIME())
                               WHEN FailedLoginCount + 1 >= 5 THEN DATEADD(MINUTE, 1, SYSUTCDATETIME())
                               ELSE LockoutUntilUtc
            END
    WHERE AccountId = @AccountId;
END;
