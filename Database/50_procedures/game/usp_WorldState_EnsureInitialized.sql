-- database/50_procedures/game/usp_WorldState_EnsureInitialized.sql
-- Contract: idempotent bootstrap -- creates the game.WorldState singleton row (Id=1, all-defaults) and the
-- 4 game.WorldStateTribes rows (TribeId 0-3, HasSymbol=1/identity-mapped per legacy default) the first time
-- it is ever called; a no-op on every later call. Meant to be called once by GameServer at world startup,
-- since these tables start empty (no seed script -- see game.WorldState) and the application can only ever
-- reach them through a procedure.
-- Result set: none. Idempotent: yes.
CREATE PROCEDURE game.usp_WorldState_EnsureInitialized
    AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
NOT EXISTS (SELECT 1 FROM game.WorldState WHERE Id = 1)
        INSERT INTO game.WorldState (Id) VALUES (1);

    IF
NOT EXISTS (SELECT 1 FROM game.WorldStateTribes)
BEGIN
INSERT INTO game.WorldStateTribes (TribeId, SymbolDate, HasSymbol, Points, IsClosed)
VALUES (0, NULL, 1, 0, 0),
       (1, NULL, 1, 0, 0),
       (2, NULL, 1, 0, 0),
       (3, NULL, 1, 0, 0);
END;
END;
