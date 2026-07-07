CREATE PROCEDURE world.usp_Zone_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ZoneNumber, DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
    FROM world.Zones
    ORDER BY ZoneNumber;
END;
