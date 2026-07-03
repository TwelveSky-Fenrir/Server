-- Contract: @LoginName NVARCHAR(64), @PasswordHash VARBINARY(32), @PasswordSalt VARBINARY(16)
--           -> RS0 { AccountId INT } (scalar, SCOPE_IDENTITY of the newly created row).
-- Not idempotent: a second call with the same @LoginName raises 50101 instead of creating a duplicate.
-- Errors: THROW 50101 if @LoginName is already taken (admin.ErrorCatalog, 501xx = auth range). The
-- check is an explicit SELECT BEFORE the INSERT -- no try/catch around the UNIQUE constraint
-- (architecture reference §12.3: constraint violations are not control flow).
CREATE PROCEDURE auth.usp_Account_Create @LoginName    NVARCHAR(64),
    @PasswordHash VARBINARY(32),
    @PasswordSalt VARBINARY(16)
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM auth.Accounts WHERE LoginName = @LoginName)
        THROW 50101, N'Account login name already taken.', 1;

INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt)
VALUES (@LoginName, @PasswordHash, @PasswordSalt);

SELECT CAST(SCOPE_IDENTITY() AS INT) AS AccountId;
END;
