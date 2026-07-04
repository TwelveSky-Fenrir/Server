-- database/50_procedures/game/usp_TribeBank_GetByTribe.sql
-- Replaces the legacy TRIBE_BANK_INFO.DAT load / ZONE_TRIBE_BANK_VIEW_FOR_PLAYUSER read path. A slot
-- with no deposit history simply has no row (implicitly 0).
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
