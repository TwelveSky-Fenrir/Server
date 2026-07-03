-- Contract: no parameters -> RS0 rows { ShardId TINYINT, Host NVARCHAR(64), Port INT, Ccu INT,
--           Capacity INT, TickP99Ms REAL }, one row per shard whose LastHeartbeatUtc falls within
-- the last 15 seconds -- older rows are silently excluded (architecture reference §9.2: "un shard
-- sans heartbeat depuis 15 s disparait de l'offre").
-- Read-only, safe to retry; the caller (LoginServer) wraps this call with a 2 s InMemoryCache
-- (architecture reference §11.4), so this proc executes at most once every 2 s regardless of login
-- throughput.
CREATE PROCEDURE runtime.usp_GameServer_GetDirectory
WITH NATIVE_COMPILATION,
     SCHEMABINDING
         AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
SELECT ShardId, Host, Port, Ccu, Capacity, TickP99Ms
FROM runtime.GameServerDirectory
WHERE LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME());
END;
