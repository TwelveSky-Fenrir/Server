-- Contract: no parameters -> RS0 rows { ZoneNumber SMALLINT, SlotIndex SMALLINT, FromZoneNumber
--           SMALLINT NULL, PosX REAL, PosY REAL, PosZ REAL }, one row per populated inbound-landing slot
--           (413 rows in this build), ordered so all of one zone's slots are contiguous. FromZoneNumber
--           is NULL when the source zone is unrecorded or names a zone this build never shipped -- see
--           world.ZoneSpawnPoints' header.
-- Bulk fetch, not per-zone: called once at GameServer boot alongside world.usp_Zone_GetAll to populate
-- the in-memory zone cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_ZoneSpawnPoint_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, FromZoneNumber, PosX, PosY, PosZ
FROM world.ZoneSpawnPoints
ORDER BY ZoneNumber, SlotIndex;
END;
