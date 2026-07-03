-- Contract: no parameters -> RS0 rows { EventDefinitionId INT, EventType INT, SortKey NVARCHAR(10)
--           NULL, Rate INT, ZoneNumber SMALLINT NULL, Message NVARCHAR(60) NULL }, every row.
-- Currently always empty (0 rows in the legacy dump, no seed data -- see
-- 30_tables/world/EventDefinitions.sql); this proc exists so future GM tooling has a stable
-- boot-time/refresh read path from day one, matching every other world.* table's access pattern.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_EventDefinition_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT EventDefinitionId, EventType, SortKey, Rate, ZoneNumber, Message
FROM world.EventDefinitions
ORDER BY EventDefinitionId;
END;
