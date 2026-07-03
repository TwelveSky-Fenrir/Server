-- Standalone IP blocklist (legacy `blockip`: rowID, uIP varchar(15)) -- no FK to anything, so unlike
-- admin.Bans this table is free to be memory-optimized. Checked on literally every inbound connection
-- attempt at both LoginServer and GameServer (the earliest possible reject, before any account lookup),
-- which is exactly the "hottest read on the connection path" shape runtime.SessionTickets/
-- game.TribeBank already use native compilation for -- so this gets the same treatment: SCHEMA_AND_DATA
-- (unlike SessionTickets' SCHEMA_ONLY) because a blocked IP is real, deliberately-persisted moderation
-- state that must survive a restart, not ephemeral session state.
-- Widened from legacy's IPv4-only varchar(15) to VARCHAR(45) to fit a fully-expanded IPv6 literal
-- (from-scratch rewrite, no reason to inherit the v4-only limitation).
-- BlockedIpId keeps the identity-surrogate-PK shape the cross-domain contract expects of every admin
-- table; the actual hot lookup is the UNIQUE HASH index on IpAddress (pure equality access pattern,
-- textbook hash-index case per SQL Server's own memory-optimized indexing guidance). No BIN2/special
-- collation needed: SQL Server 2016+ lifted the earlier collation restrictions specific to In-Memory OLTP.
CREATE TABLE admin.BlockedIps
(
    BlockedIpId  INT IDENTITY(1,1) NOT NULL,
    IpAddress    VARCHAR(45) NOT NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_BlockedIps_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BlockedIps PRIMARY KEY NONCLUSTERED (BlockedIpId),
    CONSTRAINT UQ_BlockedIps_IpAddress UNIQUE NONCLUSTERED HASH (IpAddress)
        WITH (BUCKET_COUNT = 4096) -- generous headroom over realistic M1-scale blocklist growth
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
