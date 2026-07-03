-- database/50_procedures/game/usp_TowerState_SetController.sql
-- Contract: set (or clear) which tribe controls one tower, e.g. on a successful/lost siege.
-- Params:
--   @TowerIndex         TINYINT      -- 0-11
--   @ControllingTribeId TINYINT NULL -- game.Tribes.TribeId; NULL to mark the tower uncontrolled
-- Result set: none.
-- Idempotent: yes -- re-setting the same controller repeatedly is a no-op in effect (CapturedAtUtc is
-- refreshed each call while a non-NULL controller is set, mirroring a real capture event each time).
CREATE PROCEDURE game.usp_TowerState_SetController @TowerIndex         TINYINT,
    @ControllingTribeId TINYINT = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.TowerState
SET ControllingTribeId = @ControllingTribeId,
    CapturedAtUtc      = CASE WHEN @ControllingTribeId IS NULL THEN NULL ELSE SYSUTCDATETIME() END
WHERE TowerIndex = @TowerIndex;
END;
