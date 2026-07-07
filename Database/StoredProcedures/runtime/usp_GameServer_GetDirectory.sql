-- Rows older than 15s (no recent heartbeat) are silently excluded from the shard offer. Caller
-- (LoginServer) wraps this with a 2s in-memory cache, so it executes at most once every 2s.
CREATE PROCEDURE runtime.usp_GameServer_GetDirectory
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT ShardId, Host, Port, Ccu, Capacity, TickP99Ms
    FROM runtime.GameServerDirectory
    WHERE LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME());
END;
