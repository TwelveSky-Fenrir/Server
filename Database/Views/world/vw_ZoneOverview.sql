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
