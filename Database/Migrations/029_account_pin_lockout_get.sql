-- Fix-forward, not an in-place edit: StoredProcedures/auth/usp_AccountPin_Get.sql is already
-- applied/journaled, so this corrective script CREATE OR ALTERs the same procedure name/signature from a
-- brand-new file instead of touching the original (see Database/_manifest.txt's own header and the
-- sql-server-2025-stored-procedure-conventions skill: "never edit an already-applied script").
--
-- Gap this closes (pincode-second-password security audit): exposes the two new
-- auth.AccountPins.FailedAttempts/LockedUntilUtc columns added by 028_account_pin_lockout.sql so
-- Fenrir.Data.Accounts.AccountPinRepository.GetAsync's caller (VerifyMousePinService/ChangeMousePinService)
-- can check the account-scoped lockout before ever touching PasswordHasher.Verify. Ordinal-mapped
-- (Infrastructure/Fenrir.Data.Abstractions/Accounts/AccountDtos.cs's AccountPinDto) -- new columns
-- appended at the end of the SELECT list, matching AuthenticateAccountDto's own append-at-the-end
-- convention for AccountGrade.
CREATE OR ALTER PROCEDURE auth.usp_AccountPin_Get @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT PinHash, PinSalt, FailedAttempts, LockedUntilUtc
    FROM auth.AccountPins
    WHERE AccountId = @AccountId;
END;
GO
