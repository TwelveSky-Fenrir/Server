-- Zone-with-child-row-counts shape, meant to be read from inside a retrieval procedure (security model:
-- views are never granted/called directly by app code -- see 60_permissions/001_roles.sql). Tooling/
-- debugging aid (e.g. "which zones have zero NPCs seeded", "which zone has the most monster spawn
-- regions") -- GameServer's own boot-time cache load uses the five per-table usp_*_GetAll procedures
-- directly, never this view.
-- LEFT JOINs throughout (a zone with zero portals, say, must still appear with PortalCount = 0, not be
-- dropped) aggregated in derived tables before joining to world.Zones, rather than one single GROUP BY
-- over a wide multi-table join, so each child table's row multiplication can't inflate another child
-- table's count (a zone with 5 NPCs and 3 portals joined naively would multiply to 15 rows before
-- aggregation). TotalMonsterSpawnCount sums Number (how many monsters each region summons), not just a
-- region-row count, since that is the number a capacity-planning/balance read actually wants.
-- world.MonsterSpawnRegions rows with a NULL ZoneNumber (dead-zone spawn definitions -- see that table's
-- header) are excluded from this view's region counts: they cannot join to any world.Zones row by
-- definition.
CREATE VIEW world.vw_ZoneOverview
AS
SELECT z.ZoneNumber,
       z.DefaultSpawnX,
       z.DefaultSpawnY,
       z.DefaultSpawnZ,
       ISNULL(npcSpawn.NpcSpawnCount, 0)         AS NpcSpawnCount,
       ISNULL(portal.PortalCount, 0)             AS PortalCount,
       ISNULL(spawnPoint.SpawnPointCount, 0)     AS SpawnPointCount,
       ISNULL(region.MonsterSpawnRegionCount, 0) AS MonsterSpawnRegionCount,
       ISNULL(region.TotalMonsterSpawnCount, 0)  AS TotalMonsterSpawnCount
FROM world.Zones z
         LEFT JOIN (SELECT ZoneNumber, COUNT_BIG(*) AS NpcSpawnCount
                    FROM world.ZoneNpcSpawns
                    GROUP BY ZoneNumber) npcSpawn ON npcSpawn.ZoneNumber = z.ZoneNumber
         LEFT JOIN (SELECT ZoneNumber, COUNT_BIG(*) AS PortalCount
                    FROM world.ZonePortals
                    GROUP BY ZoneNumber) portal ON portal.ZoneNumber = z.ZoneNumber
         LEFT JOIN (SELECT ZoneNumber, COUNT_BIG(*) AS SpawnPointCount
                    FROM world.ZoneSpawnPoints
                    GROUP BY ZoneNumber) spawnPoint ON spawnPoint.ZoneNumber = z.ZoneNumber
         LEFT JOIN (SELECT ZoneNumber, COUNT_BIG(*) AS MonsterSpawnRegionCount, SUM(Number) AS TotalMonsterSpawnCount
                    FROM world.MonsterSpawnRegions
                    WHERE ZoneNumber IS NOT NULL
                    GROUP BY ZoneNumber) region ON region.ZoneNumber = z.ZoneNumber;
