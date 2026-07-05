CREATE PROCEDURE world.usp_GemSocket_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT GemSocketId, Type, Value01, Value02, Value03, Value04
FROM world.GemSockets
ORDER BY GemSocketId;
END;
