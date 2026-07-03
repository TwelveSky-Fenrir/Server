-- Contract: no parameters -> RS0 rows { ZoneNumber SMALLINT, SlotIndex SMALLINT, TriggerX REAL,
--           TriggerY REAL, TriggerZ REAL, TargetZoneNumber SMALLINT NULL }, one row per populated
--           outbound-portal slot (413 rows in this build), ordered so all of one zone's slots are
--           contiguous. TargetZoneNumber is NULL for a trigger with no travel destination or one that
--           names a zone this build never shipped -- see world.ZonePortals' header.
-- Bulk fetch, not per-zone: called once at GameServer boot alongside world.usp_Zone_GetAll to populate
-- the in-memory zone cache (world.* is read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_ZonePortal_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, TriggerX, TriggerY, TriggerZ, TargetZoneNumber
FROM world.ZonePortals
ORDER BY ZoneNumber, SlotIndex;
END;
