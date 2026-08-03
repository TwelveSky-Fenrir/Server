CREATE TABLE runtime.ProxyShopExpirationRelayCursor
(
    ShardId     TINYINT NOT NULL,
    LastRelayId BIGINT  NOT NULL,
    CONSTRAINT PK_ProxyShopExpirationRelayCursor PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
