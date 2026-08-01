-- One row per live shard: the last runtime.GuildBuffExpiryRelay.RelayId that shard's own
-- GuildBuffExpiryRelayHost poll has already consumed (delivered locally, or determined did not apply, i.e.
-- its own SourceShardId). Missing row == "never polled yet", treated as 0 (usp_GuildBuffExpiryRelay_Poll).
-- Same "abandoned cursor is harmless, reap is purely time-based" posture as runtime.GuildTribeBroadcastCursor
-- -- see that table's own remarks.
CREATE TABLE runtime.GuildBuffExpiryCursor
(
    ShardId     TINYINT NOT NULL,
    LastRelayId BIGINT  NOT NULL,
    CONSTRAINT PK_GuildBuffExpiryCursor PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
