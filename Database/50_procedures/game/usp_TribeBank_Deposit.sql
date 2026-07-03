-- database/50_procedures/game/usp_TribeBank_Deposit.sql
-- Contract: atomically add @Amount to one tribe bank slot, creating the row on first deposit (replaces
-- ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND's mGAME.mTribeBankInfo[tribe][slot] += money, S04_MyWork02.cpp).
-- Params:
--   @TribeId   TINYINT -- game.Tribes.TribeId (0-3)
--   @SlotIndex TINYINT -- 0-49 (MAX_TRIBE_BANK_SLOT_NUM)
--   @Amount    INT     -- must be positive; a "deposit" of <= 0 is a caller bug, not a valid no-op
-- Result set: none.
-- Idempotent: no (each successful call increases the slot's balance by @Amount).
-- Update-then-insert-if-missing (not DELETE-then-INSERT like usp_GuildNotice_Set/usp_SessionTicket_Create
-- -- this table accumulates a value across calls instead of always replacing the whole row, so an
-- unconditional DELETE-then-INSERT would destroy the running balance instead of adding to it).
-- Natively compiled against game.TribeBank (memory-optimized, SCHEMA_AND_DATA): this is the hottest
-- write path in this whole domain (legacy: saved on every single transaction), so it runs with zero
-- locks/latches and zero T-SQL interpretation.
-- Errors:
--   THROW 50211 -- @Amount is not positive (admin.ErrorCatalog, 502xx = game range; shared with
--                  usp_TribeBank_Withdraw's identical parameter-validation case).
CREATE PROCEDURE game.usp_TribeBank_Deposit @TribeId   TINYINT,
    @SlotIndex TINYINT,
    @Amount    INT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    IF @Amount < 1
        THROW 50211, N'Tribe bank amount must be positive.', 1;

UPDATE game.TribeBank
SET Amount = Amount + @Amount
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;

IF
@@ROWCOUNT = 0
BEGIN
INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount)
VALUES (@TribeId, @SlotIndex, @Amount);
END;
END;
