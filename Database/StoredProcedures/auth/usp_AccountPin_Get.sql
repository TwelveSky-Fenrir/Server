CREATE PROCEDURE auth.usp_AccountPin_Get @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT PinHash, PinSalt, FailedLoginCount, LockoutUntilUtc
    FROM auth.AccountPins
    WHERE AccountId = @AccountId;
END;
