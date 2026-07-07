-- Password verification happens in C# (PasswordHasher), not here; this proc only reads credentials +
-- account state, and the caller reports the verdict back via auth.usp_Account_RecordLoginAttempt.
-- AccountGrade (legacy uUserSort) is loaded once here and carried forward through the session/ticket, never
-- re-queried per action.
CREATE PROCEDURE auth.usp_Account_Authenticate @LoginName NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT AccountId, PasswordHash, PasswordSalt, FailedLoginCount, LockoutUntilUtc, IsBanned, AccountGrade
    FROM auth.Accounts
    WHERE LoginName = @LoginName;
END;
