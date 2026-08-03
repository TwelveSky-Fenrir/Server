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
