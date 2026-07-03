-- Contract: @AccountId -> RS0 { PinHash VARBINARY(32), PinSalt VARBINARY(16) }
--           | empty result set if the account has no mouse PIN yet (drives "" vs "****" in LC_LOGIN_RECV
--           and the create-vs-verify branch of the PIN screen: op 13 when empty, op 15 when present).
-- Read-only: no writes, no XACT_ABORT needed. Safe to retry (pure read, no side effect).
-- Argon2id verification happens in C# (Fenrir.Domain.Security.PasswordHasher), not here -- same split as
-- auth.usp_Account_Authenticate: the proc reads credentials, the caller owns the verdict.
CREATE PROCEDURE auth.usp_AccountPin_Get @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT PinHash, PinSalt
FROM auth.AccountPins
WHERE AccountId = @AccountId;
END;
