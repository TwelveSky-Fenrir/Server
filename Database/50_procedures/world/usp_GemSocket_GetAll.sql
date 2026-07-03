-- Contract: no parameters -> RS0, one row per world.GemSockets row (2891 rows). Called once at GameServer
-- boot to populate an in-memory cache -- world.* is read-mostly reference data, never queried per-game-tick.
-- Ordered by GemSocketId (the legacy 1-based array slot position) for deterministic, cache-friendly load
-- order.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_GemSocket_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT GemSocketId, Type, Value01, Value02, Value03, Value04
FROM world.GemSockets
ORDER BY GemSocketId;
END;
