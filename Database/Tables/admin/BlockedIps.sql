-- Legacy `blockip` (IPv4-only varchar(15)); widened to VARCHAR(45) here to fit IPv6 literals.
-- UQ_BlockedIps_IpAddress's BUCKET_COUNT=4096 is sized for the expected volumetry (a blocklist of a few
-- hundred to a few thousand distinct IPs) -- per Microsoft Learn's "Configure the hash index bucket count",
-- the ideal is 1-2x the distinct key count, tolerable within ~10x over/under before lookups degrade.
-- usp_BlockedIp_Exists is the only access pattern (equality lookup on every inbound connection attempt), so
-- this index is exactly the case a hash index is built for; no action needed until the real distinct-IP
-- count is known. Monitor with
-- `SELECT * FROM sys.dm_db_xtp_hash_index_stats WHERE object_id = OBJECT_ID('admin.BlockedIps')`; revisit if
-- avg_chain_length climbs past ~10 with a low empty-bucket percent (distinct entries approaching/exceeding
-- roughly 4-8k). The fix at that point is
-- `ALTER TABLE admin.BlockedIps ALTER INDEX UQ_BlockedIps_IpAddress REBUILD WITH (BUCKET_COUNT = <n>);` --
-- note this specific ALTER TABLE form runs OFFLINE (the table is unavailable for the duration), so schedule
-- it deliberately rather than as routine online maintenance, given this table gates every connection attempt.
CREATE TABLE admin.BlockedIps
(
    BlockedIpId  INT IDENTITY (1,1) NOT NULL,
    IpAddress    VARCHAR(45)        NOT NULL,
    CreatedAtUtc DATETIME2(3)       NOT NULL
        CONSTRAINT DF_BlockedIps_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BlockedIps PRIMARY KEY NONCLUSTERED (BlockedIpId),
    CONSTRAINT UQ_BlockedIps_IpAddress UNIQUE NONCLUSTERED HASH (IpAddress)
        WITH (BUCKET_COUNT = 4096)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
