-- Mirrors legacy GSOCKET::Search(tType, tValue): lookup key is (Type, Value02), not GemSocketId (which is just array slot position).
CREATE PROCEDURE world.usp_GemSocket_GetByTypeAndValue @Type INT, @Value02 INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT GemSocketId, Type, Value01, Value02, Value03, Value04
    FROM world.GemSockets
    WHERE Type = @Type
      AND Value02 = @Value02;
END;
