CREATE PROCEDURE world.usp_MonsterSpawnRegion_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT MonsterSpawnRegionId,
       ZoneNumber,
       SourceFileName,
       Value01,
       MonsterId,
       Value03,
       Number,
       LocationX,
       LocationY,
       LocationZ,
       Radius
FROM world.MonsterSpawnRegions
ORDER BY MonsterSpawnRegionId;
END;
