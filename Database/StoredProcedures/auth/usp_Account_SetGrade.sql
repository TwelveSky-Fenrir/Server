CREATE PROCEDURE auth.usp_Account_SetGrade @LoginName NVARCHAR(64),
                                           @AccountGrade SMALLINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE auth.Accounts
    SET AccountGrade = @AccountGrade
    WHERE LoginName = @LoginName;

    IF @@ROWCOUNT = 0
        THROW 50102, N'Account login name not found.', 1;
END;
