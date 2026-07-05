CREATE PROCEDURE world.usp_ZoneSpawnPoint_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, FromZoneNumber, PosX, PosY, PosZ
FROM world.ZoneSpawnPoints
ORDER BY ZoneNumber, SlotIndex;
END;
