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
