-- Not idempotent: a second call with the same @LoginName raises 50101 instead of creating a duplicate
-- (checked explicitly before the insert; UQ constraint is the last-resort backstop under a race).
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
