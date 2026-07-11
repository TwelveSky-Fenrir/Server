-- One row per live shard: the last runtime.ChatCrossShardRelay.RelayId that shard's own ChatCrossShardRelayHost
-- poll has already consumed for rows addressed to it (TargetShardId = ShardId). Missing row == "never polled
-- yet", treated as 0 (usp_ChatCrossShardRelay_Poll). Modeled byte-for-byte on runtime.SocialCrossShardRelayCursor
-- -- see that table's own header for why an abandoned/dead shard's stale cursor row is harmless (reap
-- eligibility on the relay table is purely time-based, never cursor-based).
CREATE TABLE runtime.ChatCrossShardRelayCursor
(
    ShardId     TINYINT NOT NULL,
    LastRelayId BIGINT  NOT NULL,
    CONSTRAINT PK_ChatCrossShardRelayCursor PRIMARY KEY NONCLUSTERED HASH (ShardId)
        WITH (BUCKET_COUNT = 64)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
