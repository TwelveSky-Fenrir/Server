CREATE PROCEDURE auth.usp_Account_RecordLoginAttempt @AccountId INT,
                                                     @Success BIT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    -- LockoutUntilUtc is the end of the current failure-counting window, never a denial deadline: a failure
    -- inside an open window increments the count but must never extend the window, or the account can be pinned.
    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @WindowMinutes INT = 15;

    UPDATE auth.Accounts
    SET FailedLoginCount = CASE
                               WHEN @Success = 1 THEN 0
                               WHEN LockoutUntilUtc IS NULL OR LockoutUntilUtc <= @NowUtc THEN 1
                               ELSE FailedLoginCount + 1
            END,
        LockoutUntilUtc  = CASE
                               WHEN @Success = 1 THEN NULL
                               WHEN LockoutUntilUtc IS NULL OR LockoutUntilUtc <= @NowUtc
                                   THEN DATEADD(MINUTE, @WindowMinutes, @NowUtc)
                               ELSE LockoutUntilUtc
            END
    WHERE AccountId = @AccountId;
END;
