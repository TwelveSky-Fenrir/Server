-- Contract: no parameters -> RS0 rows { MonsterSpawnRegionId INT, ZoneNumber SMALLINT NULL,
--           SourceFileName NVARCHAR(100), Value01 INT, MonsterId INT NULL, Value03 INT, Number INT,
--           LocationX INT, LocationY INT, LocationZ INT, Radius INT }, every spawn-region row (21960
--           rows in this build), ordered by MonsterSpawnRegionId. ZoneNumber is NULL for a region
--           defined against a zone this build never shipped -- see world.MonsterSpawnRegions' header
--           (~49% of rows in this build).
-- Bulk fetch: called once at GameServer boot to populate the in-memory monster-spawn cache (world.* is
-- read-mostly reference data, never queried per-tick).
-- Read-only, safe to retry.
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
