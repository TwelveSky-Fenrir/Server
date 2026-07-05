CREATE PROCEDURE world.usp_EventDefinition_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT EventDefinitionId, EventType, SortKey, Rate, ZoneNumber, Message
FROM world.EventDefinitions
ORDER BY EventDefinitionId;
END;
