CREATE PROCEDURE game.usp_WorldState_EnsureInitialized
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @lockResult INT;
    EXEC @lockResult = sp_getapplock
                       @Resource = 'game.usp_WorldState_EnsureInitialized',
                       @LockMode = 'Exclusive',
                       @LockOwner = 'Transaction',
                       @LockTimeout = 30000;

    IF @lockResult < 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50100, 'usp_WorldState_EnsureInitialized: could not acquire applock within timeout.', 1;
        END;

    IF NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = 0)
        INSERT INTO game.Tribes (TribeId) VALUES (0), (1), (2), (3);

    IF NOT EXISTS (SELECT 1 FROM game.WorldState WHERE Id = 1)
        INSERT INTO game.WorldState (Id) VALUES (1);

    IF NOT EXISTS (SELECT 1 FROM game.WorldStateTribes)
        BEGIN
            INSERT INTO game.WorldStateTribes (TribeId, SymbolDateUtc, HasSymbol, Points, IsClosed)
            VALUES (0, NULL, 1, 0, 0),
                   (1, NULL, 1, 0, 0),
                   (2, NULL, 1, 0, 0),
                   (3, NULL, 1, 0, 0);
        END;

    COMMIT TRANSACTION;
END;
