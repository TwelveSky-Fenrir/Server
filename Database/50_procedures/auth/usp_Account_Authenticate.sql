-- Contract: @LoginName -> RS0 { AccountId INT, PasswordHash VARBINARY(32), PasswordSalt VARBINARY(16),
--                                FailedLoginCount INT, LockoutUntilUtc DATETIME2(3) NULL, IsBanned BIT }
--           | empty result set if LoginName is unknown.
-- Read-only: no writes, no XACT_ABORT needed. Safe to retry (pure read, no side effect).
-- Argon2id verification happens in C# (Fenrir.Domain.Security.PasswordHasher), not here -- this proc
-- only reads credentials + account state; the caller is responsible for the actual password verdict
-- and for reporting it back via auth.usp_Account_RecordLoginAttempt.
CREATE PROCEDURE auth.usp_Account_Authenticate @LoginName NVARCHAR(64)
AS
BEGIN
    SET
NOCOUNT ON;

SELECT AccountId, PasswordHash, PasswordSalt, FailedLoginCount, LockoutUntilUtc, IsBanned
FROM auth.Accounts
WHERE LoginName = @LoginName;
END;
