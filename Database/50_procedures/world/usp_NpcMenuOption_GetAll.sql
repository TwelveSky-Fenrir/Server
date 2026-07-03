-- Contract: no parameters -> RS0 rows { NpcId INT, SlotIndex SMALLINT, OptionId INT }, one row per menu
--           slot (13100 rows in this build -- see world.NpcMenuOptions header for why that is every slot,
--           not a sparse subset), ordered so all of one NPC's slots are contiguous.
-- Bulk fetch, not per-NPC: called once at GameServer boot alongside world.usp_Npc_GetAll to populate the
-- in-memory NPC cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_NpcMenuOption_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId, SlotIndex, OptionId
FROM world.NpcMenuOptions
ORDER BY NpcId, SlotIndex;
END;
