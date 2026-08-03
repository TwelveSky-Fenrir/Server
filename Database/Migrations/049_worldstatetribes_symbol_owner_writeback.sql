
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('game.WorldStateTribes')
                 AND name = 'SymbolOwnerTribeId')
    ALTER TABLE game.WorldStateTribes
        ADD SymbolOwnerTribeId TINYINT NOT NULL
            CONSTRAINT DF_WorldStateTribes_SymbolOwnerTribeId DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1
               FROM sys.foreign_keys
               WHERE name = N'FK_WorldStateTribes_SymbolOwnerTribeId')
    ALTER TABLE game.WorldStateTribes
        WITH CHECK
            ADD CONSTRAINT FK_WorldStateTribes_SymbolOwnerTribeId FOREIGN KEY (SymbolOwnerTribeId)
                REFERENCES game.Tribes (TribeId);
GO

UPDATE game.WorldStateTribes
SET SymbolOwnerTribeId = TribeId
WHERE SymbolOwnerTribeId <> TribeId;
GO

CREATE OR ALTER PROCEDURE game.usp_WorldState_EnsureInitialized
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
            INSERT INTO game.WorldStateTribes (TribeId, SymbolDateUtc, HasSymbol, Points, IsClosed,
                                                SymbolOwnerTribeId)
            VALUES (0, NULL, 1, 0, 0, 0),
                   (1, NULL, 1, 0, 0, 1),
                   (2, NULL, 1, 0, 0, 2),
                   (3, NULL, 1, 0, 0, 3);
        END;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_WorldState_Get
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id,
           Zone038WinTribe,
           Zone038WinTribeTime,
           TribeSymbolBattle,
           MonsterSymbol,
           MonsterSymbolEndTime,
           HighTribe,
           UpdateTribePoint,
           UpdatedAtUtc
    FROM game.WorldState;

    SELECT TribeId, SymbolDateUtc, HasSymbol, Points, IsClosed, SymbolOwnerTribeId
    FROM game.WorldStateTribes
    ORDER BY TribeId;

    SELECT FromTribeId, ToTribeId, IsAccepted
    FROM game.WorldStateAllianceOffers
    ORDER BY FromTribeId, ToTribeId;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_WorldStateTribe_Update @TribeId TINYINT,
                                                          @SymbolDateUtc DATETIME2(3) = NULL,
                                                          @HasSymbol BIT,
                                                          @Points INT,
                                                          @IsClosed BIT,
                                                          @SymbolOwnerTribeId TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET SymbolDateUtc      = @SymbolDateUtc,
        HasSymbol          = @HasSymbol,
        Points             = @Points,
        IsClosed           = @IsClosed,
        SymbolOwnerTribeId = @SymbolOwnerTribeId
    WHERE TribeId = @TribeId;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_WorldStateTribe_UpdateSymbolState @TribeId TINYINT,
                                                                     @SymbolDateUtc DATETIME2(3) = NULL,
                                                                     @HasSymbol BIT,
                                                                     @IsClosed BIT,
                                                                     @SymbolOwnerTribeId TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET SymbolDateUtc      = @SymbolDateUtc,
        HasSymbol          = @HasSymbol,
        IsClosed           = @IsClosed,
        SymbolOwnerTribeId = @SymbolOwnerTribeId
    WHERE TribeId = @TribeId;
END;
GO
