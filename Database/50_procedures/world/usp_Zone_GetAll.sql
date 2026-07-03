-- Contract: no parameters -> RS0 rows { ZoneNumber SMALLINT, DefaultSpawnX REAL, DefaultSpawnY REAL,
--           DefaultSpawnZ REAL }, every live zone (117 rows in this build), ordered by ZoneNumber.
-- Called once at GameServer boot to populate the in-memory zone cache -- world.* is read-mostly
-- reference data, never queried per-tick (see 60_permissions/001_roles.sql).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Zone_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
FROM world.Zones
ORDER BY ZoneNumber;
END;
