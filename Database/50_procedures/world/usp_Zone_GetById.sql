-- Contract: @ZoneNumber SMALLINT -> RS0 zero or one row { ZoneNumber, DefaultSpawnX, DefaultSpawnY,
--           DefaultSpawnZ }. Tooling/debugging helper only -- GameServer itself always loads the full
-- set via world.usp_Zone_GetAll once at boot, never a single zone per request.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_Zone_GetById @ZoneNumber SMALLINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
FROM world.Zones
WHERE ZoneNumber = @ZoneNumber;
END;
