-- Empty result set if the account has no mouse PIN yet -- drives the create-vs-verify branch of the
-- PIN screen (op 13 when empty, op 15 when present).
CREATE PROCEDURE auth.usp_AccountPin_Get @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT PinHash, PinSalt
FROM auth.AccountPins
WHERE AccountId = @AccountId;
END;
