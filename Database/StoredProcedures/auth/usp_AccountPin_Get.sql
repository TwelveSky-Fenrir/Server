-- Empty result set if the account has no mouse PIN yet -- drives the create-vs-verify branch of the
-- PIN screen (op 13 when empty, op 15 when present).
--
-- Also exposes auth.AccountPins.FailedLoginCount/LockoutUntilUtc so
-- Fenrir.Data.Accounts.AccountPinRepository.GetAsync's caller (VerifyMousePinService/ChangeMousePinService)
-- can check the account-scoped lockout before ever touching PasswordHasher.Verify. Ordinal-mapped
-- (Infrastructure/Fenrir.Data.Abstractions/Accounts/AccountDtos.cs's AccountPinDto, whose own
-- FailedAttempts/LockedUntilUtc property names predate this table's rename and are intentionally left as
-- the C# projection's own names -- GenerateDto binds by column order, not by name, so no coupling forces
-- them to match) -- new columns appended at the end of the SELECT list, matching AuthenticateAccountDto's
-- own append-at-the-end convention for AccountGrade.
CREATE PROCEDURE auth.usp_AccountPin_Get @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT PinHash, PinSalt, FailedLoginCount, LockoutUntilUtc
    FROM auth.AccountPins
    WHERE AccountId = @AccountId;
END;
