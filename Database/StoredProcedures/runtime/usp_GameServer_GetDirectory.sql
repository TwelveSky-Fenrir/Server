-- Rows older than @StalenessCutoffSeconds (no recent heartbeat) are silently excluded from the shard offer.
-- Caller (LoginServer) wraps this with a 2s in-memory cache, so it executes at most once every 2s.
-- @StalenessCutoffSeconds defaults to 15 (three heartbeats at GameServerOptions' default 5s interval) so
-- every existing caller keeps working unmodified with the same cutoff as before; a caller that knows its own
-- configured heartbeat cadence can pass an explicit value instead of relying on the default matching it.
CREATE PROCEDURE runtime.usp_GameServer_GetDirectory @StalenessCutoffSeconds INT = 15
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT ShardId, Host, Port, Ccu, Capacity, TickP99Ms
    FROM runtime.GameServerDirectory
    WHERE LastHeartbeatUtc > DATEADD(SECOND, -@StalenessCutoffSeconds, SYSUTCDATETIME());
END;
