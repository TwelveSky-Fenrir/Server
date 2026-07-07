-- Live-shard cache kept warm by usp_GameServer_Heartbeat; a crashed shard just stops heartbeating
-- (nothing to recover from disk) and is aged out of usp_GameServer_GetDirectory's results by its
-- LastHeartbeatUtc filter (no physical row deletion).
-- PK ShardId is assigned by each shard's own static config, never generated here.
CREATE TABLE runtime.GameServerDirectory
(
    ShardId          TINYINT      NOT NULL,
    Host             NVARCHAR(64) NOT NULL,
    Port             INT          NOT NULL,
    Ccu              INT          NOT NULL,
    Capacity         INT          NOT NULL,
    TickP99Ms        REAL         NOT NULL,
    LastHeartbeatUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_GameServerDirectory PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
