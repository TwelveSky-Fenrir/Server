-- database/50_procedures/game/usp_TowerState_SetController.sql
-- Idempotent; CapturedAtUtc refreshes on every call that sets a non-NULL controller.
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
