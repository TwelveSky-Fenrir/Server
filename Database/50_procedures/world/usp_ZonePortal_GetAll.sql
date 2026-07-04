CREATE PROCEDURE world.usp_ZonePortal_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ZoneNumber, SlotIndex, TriggerX, TriggerY, TriggerZ, TargetZoneNumber
FROM world.ZonePortals
ORDER BY ZoneNumber, SlotIndex;
END;
