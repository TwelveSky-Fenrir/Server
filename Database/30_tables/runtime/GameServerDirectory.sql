-- One row per live game-server shard, kept warm by usp_GameServer_Heartbeat so Login can pick a
-- destination for usp_SessionTicket_Create without a fan-out RPC to every shard on each login.
-- SCHEMA_ONLY (architecture reference §12.4, same rationale as SessionTickets.sql): this is a cache
-- of live process state -- a shard that crashed has nothing worth recovering from disk, it just
-- stops heartbeating and usp_GameServer_Purge (ops-side, not M1) ages the row out.
-- PK on ShardId, not an identity: the shard's own static config assigns its id, the directory only
-- ever looks it up, never generates one.
CREATE TABLE runtime.GameServerDirectory
(
    ShardId          TINYINT NOT NULL,
    Host             NVARCHAR(64) NOT NULL,
    Port             INT     NOT NULL,
    Ccu              INT     NOT NULL,
    Capacity         INT     NOT NULL,
    TickP99Ms        REAL    NOT NULL,
    LastHeartbeatUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_GameServerDirectory PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64) -- realistic upper bound on concurrently running shards, not CCU
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
