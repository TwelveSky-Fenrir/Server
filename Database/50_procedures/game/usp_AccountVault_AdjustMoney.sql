-- database/50_procedures/game/usp_AccountVault_AdjustMoney.sql
-- Contract: atomically add (positive) or subtract (negative) both money pools in one round trip; rejects
-- (does not clamp to 0) an adjustment that would drive either pool negative -- same explicit
-- raise-on-insufficient-funds philosophy as game.usp_TribeBank_Withdraw, so a caller relying on "the
-- withdrawal happened" without checking cannot silently under-withdraw.
-- Params: @AccountId INT, @DeltaMoney BIGINT, @DeltaMoney2 BIGINT (either may be 0; either may be negative).
-- Result set: none.
-- Idempotent: no (each successful call changes the balance by the given delta).
-- Single guarded UPDATE (SET Money = Money + @DeltaMoney), not read-then-write: atomic under concurrent
-- callers for the same account within one statement, same as game.usp_Character_PersistBatch's guarded
-- UPDATE idiom. A missing row (game.usp_AccountVault_EnsureInitialized never called for this account) is
-- treated as insufficient funds -- there is nothing to withdraw from a vault that was never initialized.
-- Errors:
--   THROW 50221 -- the adjustment would drive Money or Money2 negative (admin.ErrorCatalog, 502xx = game
--                  range).
CREATE PROCEDURE game.usp_AccountVault_AdjustMoney @AccountId   INT,
    @DeltaMoney  BIGINT,
    @DeltaMoney2 BIGINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.AccountVault
SET Money        = Money + @DeltaMoney,
    Money2       = Money2 + @DeltaMoney2,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AccountId = @AccountId
  AND Money + @DeltaMoney >= 0
  AND Money2 + @DeltaMoney2 >= 0;

IF
@@ROWCOUNT = 0
        THROW 50221, N'Insufficient account vault balance for this adjustment.', 1;
END;
