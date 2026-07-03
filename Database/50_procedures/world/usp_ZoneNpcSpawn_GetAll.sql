-- Contract: no parameters -> RS0 rows { ZoneNumber SMALLINT, SlotIndex SMALLINT, NpcId INT NULL,
--           PosX REAL, PosY REAL, PosZ REAL, Angle REAL }, one row per populated NPC-placement slot
--           (291 rows in this build), ordered so all of one zone's slots are contiguous.
-- Bulk fetch, not per-zone: called once at GameServer boot alongside world.usp_Zone_GetAll to populate
-- the in-memory zone cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_ZoneNpcSpawn_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, NpcId, PosX, PosY, PosZ, Angle
FROM world.ZoneNpcSpawns
ORDER BY ZoneNumber, SlotIndex;
END;
