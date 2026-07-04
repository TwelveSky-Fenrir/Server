-- database/50_procedures/game/usp_TribeBank_Withdraw.sql
-- Folds the legacy PlayUser process's single-operation withdraw+credit
-- (ZONE_TRIBE_BANK_LOAD_FOR_PLAYUSER_SEND) into one transaction: Fenrir splits the tribe bank and character
-- money across two tables, so a mid-sequence failure must never debit one without crediting the other.
-- Always empties the whole slot (matches the legacy's own mGAME.mTribeBankInfo[tribe][slot] = 0), never a
-- partial withdrawal. Not natively compiled (unlike usp_TribeBank_Deposit): a natively compiled module
-- cannot touch the disk-based game.Characters table. game.TribeBank access needs WITH (SNAPSHOT) for the
-- same cross-container reason as usp_Guild_Disband.
CREATE PROCEDURE game.usp_TribeBank_Withdraw @TribeId     TINYINT,
    @SlotIndex   TINYINT,
    @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@Amount INT;

BEGIN
TRANSACTION;

SELECT @Amount = Amount
FROM game.TribeBank WITH (SNAPSHOT)
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;

IF
@Amount IS NULL OR @Amount < 1
        THROW 50210, N'Insufficient tribe bank balance for this withdrawal.', 1;

UPDATE game.TribeBank
WITH (SNAPSHOT)
SET Amount = 0
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;

UPDATE game.Characters
SET Money        = Money + @Amount,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND Money + @Amount BETWEEN 0 AND 2000000000;

IF
@@ROWCOUNT = 0
        THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

COMMIT TRANSACTION;

SELECT Money
FROM game.Characters
WHERE CharacterId = @CharacterId;
END;
