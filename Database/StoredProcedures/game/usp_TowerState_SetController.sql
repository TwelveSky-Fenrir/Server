CREATE PROCEDURE game.usp_TowerState_SetController @TowerIndex TINYINT,
                                                   @Level TINYINT,
                                                   @TowerType TINYINT,
                                                   @AttackState SMALLINT,
                                                   @ControllingTribeId TINYINT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.TowerState
    SET Level              = @Level,
        TowerType          = @TowerType,
        AttackState        = @AttackState,
        ControllingTribeId = @ControllingTribeId,
        CapturedAtUtc      = CASE WHEN @ControllingTribeId IS NULL THEN NULL ELSE SYSUTCDATETIME() END
    WHERE TowerIndex = @TowerIndex;
END;
