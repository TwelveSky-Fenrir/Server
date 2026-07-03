-- database/50_procedures/game/usp_TribeSubMaster_GetByTribe.sql
-- Contract: the up-to-12 sub-master slots for one tribe (see game.TribeSubMasters for why this is a
-- child table rather than 2 fixed columns on game.Tribes).
-- Params:
--   @TribeId TINYINT -- game.Tribes.TribeId (0-3)
-- Result set (RS0), ordered by SlotIndex, one row per occupied slot (0..12 rows):
--   1. TribeId     TINYINT
--   2. SlotIndex   TINYINT (0-11)
--   3. CharacterId INT
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @TribeId simply returns zero rows).
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
