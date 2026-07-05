-- database/50_procedures/game/usp_TribeSubMaster_GetByTribe.sql
-- Up to 12 sub-master slots per tribe, ordered by SlotIndex.
CREATE PROCEDURE game.usp_TribeSubMaster_GetByTribe @TribeId TINYINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT TribeId,
       SlotIndex,
       CharacterId
FROM game.TribeSubMasters
WHERE TribeId = @TribeId
ORDER BY SlotIndex;
END;
