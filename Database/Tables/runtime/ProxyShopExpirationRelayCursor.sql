-- One row per live shard: the last runtime.ProxyShopExpirationRelay.RelayId that shard's own
-- ProxyShopExpirationRelayHost poll has already consumed (delivered locally, or determined did not apply,
-- i.e. its own SourceShardId). Missing row == "never polled yet", treated as 0
-- (usp_ProxyShopExpirationRelay_Poll). Modeled byte-for-byte on runtime.GuildTribeBroadcastCursor -- see that
-- table's own header for why an abandoned/dead shard's stale cursor row is harmless (reap eligibility on the
-- relay table is purely time-based, never cursor-based).
CREATE TABLE runtime.ProxyShopExpirationRelayCursor
(
    ShardId     TINYINT NOT NULL,
    LastRelayId BIGINT  NOT NULL,
    CONSTRAINT PK_ProxyShopExpirationRelayCursor PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
