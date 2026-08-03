CREATE PROCEDURE game.usp_TowerState_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TowerIndex, Level, TowerType, ControllingTribeId, CapturedAtUtc
    FROM game.TowerState
    ORDER BY TowerIndex;
END;
