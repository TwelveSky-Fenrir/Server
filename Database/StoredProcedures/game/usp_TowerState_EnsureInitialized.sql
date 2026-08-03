CREATE PROCEDURE game.usp_TowerState_EnsureInitialized
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        NOT EXISTS (SELECT 1 FROM game.TowerState)
        BEGIN
            INSERT INTO game.TowerState (TowerIndex, ControllingTribeId, CapturedAtUtc)
            VALUES (0, NULL, NULL),
                   (1, NULL, NULL),
                   (2, NULL, NULL),
                   (3, NULL, NULL),
                   (4, NULL, NULL),
                   (5, NULL, NULL),
                   (6, NULL, NULL),
                   (7, NULL, NULL),
                   (8, NULL, NULL),
                   (9, NULL, NULL),
                   (10, NULL, NULL),
                   (11, NULL, NULL);
        END;
END;
