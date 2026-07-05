-- database/50_procedures/game/usp_TribeBank_Deposit.sql
-- Replaces ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND's mGAME.mTribeBankInfo[tribe][slot] += money handling.
-- Update-then-insert-if-missing (not DELETE-then-INSERT): this table accumulates a running balance
-- across calls, unlike most other upsert procs in this schema.
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
