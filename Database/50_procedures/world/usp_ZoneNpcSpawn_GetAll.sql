CREATE PROCEDURE world.usp_ZoneNpcSpawn_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, NpcId, PosX, PosY, PosZ, Angle
FROM world.ZoneNpcSpawns
ORDER BY ZoneNumber, SlotIndex;
END;
