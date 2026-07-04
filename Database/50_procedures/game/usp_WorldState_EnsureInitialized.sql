-- database/50_procedures/game/usp_WorldState_EnsureInitialized.sql
-- Idempotent bootstrap: seeds the WorldState singleton + 4 WorldStateTribes rows on first call only. Call
-- once at GameServer startup.
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
