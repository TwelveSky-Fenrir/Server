CREATE PROCEDURE game.usp_TowerState_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TowerIndex, Level, TowerType, AttackState, ControllingTribeId, CapturedAtUtc
    FROM game.TowerState
    ORDER BY TowerIndex;
END;
