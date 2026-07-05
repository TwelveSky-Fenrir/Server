-- database/50_procedures/game/usp_WorldState_Get.sql
-- 3 result sets in one round trip (WorldState, WorldStateTribes, WorldStateAllianceOffers), loaded at
-- GameServer boot.
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
