-- game.WorldStateTribes.HasSymbol only ever tracked a per-tribe boolean (does this tribe hold ITS OWN
-- symbol slot?), never the actual captor tribe id -- so a captured slot (winner != slot's home tribe)
-- could not round-trip through a restart at all, and WorldStateService.InitializeAsync papered over the
-- gap by unconditionally resetting the in-memory _tribeSymbolOwner[] to identity on every boot, discarding
-- whatever a symbol battle had actually decided (legacy: Server/ts25center/S07_MyGame01.cpp:146-160 restores
-- mTribeSymbol[] from the DB row; Fenrir never did). Adds the missing per-slot captor column, reusing the
-- existing WorldStateTribes table/round-trip rather than a parallel mechanism (one row per slot already
-- exists, TribeId is the slot's home tribe).
--
-- usp_WorldState_EnsureInitialized/usp_WorldState_Get/usp_WorldStateTribe_Update/
-- usp_WorldStateTribe_UpdateSymbolState are already SHA-256-journaled by their own base scripts, so they are
-- redefined here via CREATE OR ALTER rather than edited in place -- same idiom as
-- Migrations/046_ornament_silver_gold_time_columns.sql's CREATE OR ALTER of usp_Character_GetForWorldEntry.

-- 0. Add the column (idempotent). DEFAULT 0 only satisfies the NOT NULL add for pre-existing rows; the
--    backfill below sets it to the correct per-row identity value (SymbolOwnerTribeId = TribeId), and every
--    future seed row is written explicitly by the redefined EnsureInitialized below.
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

-- 1. Backfill: this migration is the introduction of the column, so every row currently in the table is
--    either the fresh identity seed from usp_WorldState_EnsureInitialized or a slot nobody has captured yet
--    -- identity is the correct value for both, matching the legacy identity default
--    (Server/ts25center/S07_MyGame01.cpp:24-27, Server/BuildEU33/DB/nxtserver.sql:1307-1363's seed row).
UPDATE game.WorldStateTribes
SET SymbolOwnerTribeId = TribeId
WHERE SymbolOwnerTribeId <> TribeId;
GO

-- 2. Seed path: redefine EnsureInitialized so a FRESH database seeds SymbolOwnerTribeId = TribeId per row
--    (a single column-level DEFAULT constant cannot vary per row 0/1/2/3, so the seed INSERT must list it
--    explicitly). Verbatim copy of the base script's sp_getapplock body otherwise.
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

-- 3. Read path (boot-time restore / periodic reconcile).
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

-- 4. Full-record write path (TryOverwriteTribePointTotalsAsync's immediate persist).
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

-- 5. Symbol-state write-behind path (WorldStateService.FlushIfDirtyAsync's periodic flush).
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
