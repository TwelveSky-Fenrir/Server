-- database/50_procedures/game/usp_TribeBank_Withdraw.sql
-- Rejects (never clamps) a withdrawal that would drive the balance negative; replaces the withdraw half
-- of ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND's mGAME.mTribeBankInfo[tribe][slot] handling.
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
