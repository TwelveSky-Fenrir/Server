-- One row per live shard: the last runtime.GuildTribeBroadcastRelay.RelayId that shard's own
-- GuildTribeBroadcastRelayHost poll has already consumed (delivered locally, or determined did not apply,
-- i.e. its own SourceShardId). Missing row == "never polled yet", treated as 0
-- (usp_GuildTribeBroadcastRelay_Poll). An abandoned/dead shard's cursor row simply goes stale and is
-- harmless -- it plays no part in reap eligibility, which is purely time-based (see
-- GuildTribeBroadcastRelay's own remarks for why).
CREATE TABLE runtime.GuildTribeBroadcastCursor
(
    ShardId     TINYINT NOT NULL,
    LastRelayId BIGINT  NOT NULL,
    CONSTRAINT PK_GuildTribeBroadcastCursor PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
