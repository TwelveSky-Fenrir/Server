-- database/50_procedures/game/usp_TowerState_GetAll.sql
-- Contract: no parameters -> RS0 { TowerIndex, Level, TowerType, ControllingTribeId, CapturedAtUtc }, one row per
-- tower (0-11 once game.usp_TowerState_EnsureInitialized has run), ordered by TowerIndex.
-- Read-only, safe to retry.
CREATE PROCEDURE game.usp_TowerState_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TowerIndex, Level, TowerType, ControllingTribeId, CapturedAtUtc
    FROM game.TowerState
    ORDER BY TowerIndex;
END;
