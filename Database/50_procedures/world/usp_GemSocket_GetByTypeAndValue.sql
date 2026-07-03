-- Contract: @Type INT, @Value02 INT -> RS0 zero or one row.
-- Tooling/debugging helper mirroring the actual legacy lookup shape: GSOCKET::Search(int tType, int tValue)
-- in ts25zone/GameSystem/GameSystem_08_Socket.cpp matches on exactly (mType, mValue02), which real data
-- confirms is a genuine unique key (UQ_GemSockets_Type_Value02) -- not GemSocketId, which is just the array
-- slot position and has no gameplay meaning of its own. GameServer's own boot-time cache load always uses
-- world.usp_GemSocket_GetAll, never this per-lookup proc.
-- Read-only, safe to retry.
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
