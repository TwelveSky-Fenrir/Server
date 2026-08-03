CREATE PROCEDURE auth.usp_Account_Create @LoginName NVARCHAR(64),
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

    BEGIN TRY
        INSERT INTO auth.Accounts (LoginName, PasswordHash, PasswordSalt)
        VALUES (@LoginName, @PasswordHash, @PasswordSalt);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2627, 2601)
            THROW;

        THROW 50101, N'Account login name already taken.', 1;
    END CATCH;

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS AccountId;
END;
