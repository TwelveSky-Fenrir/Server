-- database/50_procedures/game/usp_WorldState_Get.sql
-- Contract: no parameters -> 3 result sets, called at GameServer boot (and on demand for admin/debug
-- tooling) to load the whole contested-world state in one round trip:
--   RS0: the game.WorldState singleton row (0 or 1 rows -- 0 until
--        game.usp_WorldState_EnsureInitialized has run)
--   RS1: one row per game.WorldStateTribes (0-4 rows), ordered by TribeId
--   RS2: one row per game.WorldStateAllianceOffers (0-N rows), ordered by FromTribeId, ToTribeId
-- Read-only, safe to retry.
CREATE PROCEDURE game.usp_WorldState_Get
    AS
BEGIN
    SET
NOCOUNT ON;

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

SELECT TribeId, SymbolDate, HasSymbol, Points, IsClosed
FROM game.WorldStateTribes
ORDER BY TribeId;

SELECT FromTribeId, ToTribeId, IsAccepted
FROM game.WorldStateAllianceOffers
ORDER BY FromTribeId, ToTribeId;
END;
