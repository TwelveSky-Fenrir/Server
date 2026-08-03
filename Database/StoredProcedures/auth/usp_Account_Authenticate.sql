CREATE PROCEDURE auth.usp_Account_Authenticate @LoginName NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT AccountId, PasswordHash, PasswordSalt, FailedLoginCount, LockoutUntilUtc, IsBanned, AccountGrade
    FROM auth.Accounts
    WHERE LoginName = @LoginName;
END;
