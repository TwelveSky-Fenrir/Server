-- database/50_procedures/game/usp_TribeBank_Withdraw.sql
-- Contract: atomically subtract @Amount from one tribe bank slot; rejects (does not clamp to 0) a
-- withdrawal that would drive the balance negative (replaces the withdraw half of
-- ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND's mGAME.mTribeBankInfo[tribe][slot] handling).
-- Params:
--   @TribeId   TINYINT -- game.Tribes.TribeId (0-3)
--   @SlotIndex TINYINT -- 0-49 (MAX_TRIBE_BANK_SLOT_NUM)
--   @Amount    INT     -- must be positive
-- Result set: none.
-- Idempotent: no (each successful call decreases the slot's balance by @Amount).
-- Explicit raise-on-insufficient-funds (per the task brief), not a silent clamp to 0: a caller relying
-- on "the withdrawal happened" without checking would otherwise silently under-withdraw, corrupting
-- whatever inventory/currency exchange the caller is doing on the other side of the transaction.
-- A missing row (SlotIndex never deposited into) is treated identically to a balance of 0 -- there is
-- nothing to withdraw from a slot that was never funded.
-- Natively compiled against game.TribeBank (memory-optimized, SCHEMA_AND_DATA): same hot-path rationale
-- as usp_TribeBank_Deposit.
-- Errors:
--   THROW 50211 -- @Amount is not positive (admin.ErrorCatalog, 502xx = game range).
--   THROW 50210 -- withdrawal would drive the slot balance negative (admin.ErrorCatalog, 502xx).
CREATE PROCEDURE game.usp_TribeBank_Withdraw @TribeId   TINYINT,
    @SlotIndex TINYINT,
    @Amount    INT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
@Current INT;

    IF
@Amount < 1
        THROW 50211, N'Tribe bank amount must be positive.', 1;

SELECT @Current = Amount
FROM game.TribeBank
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;

IF
@Current IS NULL OR @Current < @Amount
        THROW 50210, N'Insufficient tribe bank balance for this withdrawal.', 1;

UPDATE game.TribeBank
SET Amount = Amount - @Amount
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;
END;
