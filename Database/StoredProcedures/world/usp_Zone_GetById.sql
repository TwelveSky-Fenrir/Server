CREATE PROCEDURE world.usp_Zone_GetById @ZoneNumber SMALLINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ZoneNumber, DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
    FROM world.Zones
    WHERE ZoneNumber = @ZoneNumber;
END;
