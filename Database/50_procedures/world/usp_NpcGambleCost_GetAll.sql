-- Contract: no parameters -> RS0 rows { NpcId INT, GambleTier SMALLINT, CostIndex TINYINT, Value INT },
--           one row per populated gamble-cost cell (13050 rows in this build -- see world.NpcGambleCosts
--           header for why that is every cell for the 6 NPCs that use this feature, not a sparse subset
--           of them), ordered so all of one NPC's cells are contiguous.
-- Bulk fetch, not per-NPC: called once at GameServer boot alongside world.usp_Npc_GetAll to populate the
-- in-memory NPC cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_NpcGambleCost_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId, GambleTier, CostIndex, Value
FROM world.NpcGambleCosts
ORDER BY NpcId, GambleTier, CostIndex;
END;
