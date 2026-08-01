-- database/50_procedures/game/usp_WorldState_EnsureInitialized.sql
-- Idempotent bootstrap: seeds game.Tribes 0-3 (MAX_TRIBE_NUM=4, game.Tribes has no seed script of its own --
-- Migrations/Seed only seeds admin/world) + the WorldState singleton + 4 WorldStateTribes rows on first call
-- only, in that order, since WorldStateTribes FKs into Tribes (FK_WorldStateTribes_Tribe). Call once at
-- GameServer startup.
--
-- GameServer is sharded (Orchestration/Fenrir.AppHost/AppHost.cs runs one game-shard-NN process per shard
-- id), and every shard process calls this procedure once at boot (WorldStateService.InitializeAsync,
-- Program.cs) with no external serialization between shards. Plain "IF NOT EXISTS (...) INSERT" guards are
-- not atomic under SQL Server's default READ COMMITTED isolation: two shards' EXISTS checks can both see
-- "no row yet" before either INSERT commits, so the second INSERT would hit PK_WorldState (or the
-- Tribes/WorldStateTribes equivalents) -- observed live as a CaeriusNetSqlException: "Violation of PRIMARY
-- KEY constraint 'PK_WorldState'... duplicate key value (1)" during concurrent shard boot. Fix: take an
-- exclusive sp_getapplock scoped to the transaction (auto-released on COMMIT/ROLLBACK) before the existence
-- checks, so concurrent callers serialize instead of racing -- the second caller blocks until the first
-- commits, then re-evaluates NOT EXISTS and correctly finds the rows already present.
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
