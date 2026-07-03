-- Contract: no parameters -> RS0 rows { ZoneNumber, DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ,
--           NpcSpawnCount, PortalCount, SpawnPointCount, MonsterSpawnRegionCount,
--           TotalMonsterSpawnCount }, one row per zone, ordered by ZoneNumber.
-- Tooling/debugging helper only (e.g. "which zones have zero NPCs seeded") -- reads
-- world.vw_ZoneOverview internally (security model: views are an internal T-SQL readability tool a
-- procedure's body may use -- see 60_permissions/001_roles.sql). GameServer's own boot-time cache load
-- uses the five per-table usp_*_GetAll procedures directly, never this one.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Zone_GetOverview
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber,
       DefaultSpawnX,
       DefaultSpawnY,
       DefaultSpawnZ,
       NpcSpawnCount,
       PortalCount,
       SpawnPointCount,
       MonsterSpawnRegionCount,
       TotalMonsterSpawnCount
FROM world.vw_ZoneOverview
ORDER BY ZoneNumber;
END;
