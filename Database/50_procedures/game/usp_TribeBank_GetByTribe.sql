-- database/50_procedures/game/usp_TribeBank_GetByTribe.sql
-- Contract: all 50 slot balances for one tribe (replaces the legacy TRIBE_BANK_INFO.DAT load / the
-- ZONE_TRIBE_BANK_VIEW_FOR_PLAYUSER read path, PLAYUSER.h).
-- Plain (non-native) proc: reads are not the hot path here, only Deposit/Withdraw are -- see
-- game.TribeBank for why the table itself is still memory-optimized.
-- Params:
--   @TribeId TINYINT -- game.Tribes.TribeId (0-3)
-- Result set (RS0), ordered by SlotIndex, one row per slot that has ever been deposited into (0..50
-- rows -- a slot with no deposit/withdraw history yet simply has no row, it is implicitly 0):
--   1. TribeId   TINYINT
--   2. SlotIndex TINYINT (0-49)
--   3. Amount    INT
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @TribeId simply returns zero rows).
CREATE PROCEDURE game.usp_TribeBank_GetByTribe @TribeId TINYINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT TribeId,
       SlotIndex,
       Amount
FROM game.TribeBank
WHERE TribeId = @TribeId
ORDER BY SlotIndex;
END;
