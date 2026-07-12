-- One row per account with a currently-tracked session anywhere in the cluster. SCHEMA_ONLY like
-- runtime.SessionTickets/GameServerDirectory: a crash wiping this table just means every account reads
-- as "no active session", which is the safe direction to fail toward for a duplicate-login guard.
--
-- Uniqueness is deliberately PK(AccountId) alone, not PK(AccountId, ServerKind): an account has AT MOST ONE
-- tracked session in the whole cluster, period, not one row per ServerKind. ServerKind is a mutable state
-- field on that single row's own lifecycle (Login=0 -> Game=1), flipped in place by
-- usp_AccountSession_TransitionToGame's UPDATE, never a second inserted row -- a Login(ServerKind=0) row and
-- a Game(ServerKind=1) row for the same account can never coexist by construction. This is stricter than (and
-- supersedes) "one active session per ServerKind": it also correctly rules out a Login row lingering
-- alongside a Game row for the same account during a hand-off, which a (AccountId, ServerKind) composite key
-- would still have allowed.
--
-- AdapterIdentifier/LocalIp/RemoteIp back the per-adapter concurrent-connection quota gate (legacy mac_limit/
-- adapterCount) -- populated unconditionally by usp_AccountSession_RecordDeviceSignature right after a
-- successful claim (LoginService.cs), so they read NULL only in the brief window between
-- usp_AccountSession_ClaimOrSignalKick's own INSERT and that follow-up call. See
-- usp_AccountSession_GetConcurrentDeviceCount's own index below for why this triple gets a NONCLUSTERED (not
-- HASH) secondary index despite being an equality-only lookup.
CREATE TABLE runtime.AccountSessions
(
    AccountId          INT              NOT NULL,
    ServerKind         TINYINT          NOT NULL, -- 0 = Login, 1 = Game
    ShardId            TINYINT          NULL,     -- meaningful only when ServerKind = 1
    SessionToken       UNIQUEIDENTIFIER NOT NULL, -- minted once at Login-claim time, carried through runtime.SessionTickets
    SessionState       TINYINT          NOT NULL, -- 0 = Active, 1 = TearingDown
    KickRequested      BIT              NOT NULL,
    ConnectedAtUtc     DATETIME2(3)     NOT NULL,
    LastRefreshedUtc   DATETIME2(3)     NOT NULL, -- bumped by the owning process's own periodic poll (every 2s/shard); 6-min sweep reads this
    AdapterIdentifier  VARCHAR(128)     NULL,     -- device-fingerprint half of the per-adapter connection quota
    LocalIp            VARCHAR(45)      NULL,
    RemoteIp           VARCHAR(45)      NULL,
    CONSTRAINT PK_AccountSessions PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    -- Supports usp_AccountSession_GetConcurrentDeviceCount's per-login equality lookup on all three columns
    -- (called on every login attempt that isn't GM-tier and has no per-account configured MAC limit) --
    -- without this, that lookup is a full scan of every currently-tracked session cluster-wide, on every
    -- single login. NONCLUSTERED rather than HASH: this quota gate's whole purpose is detecting MULTIPLE
    -- accounts sharing one device signature, i.e. duplicate key values are the expected, common case here,
    -- and hash indexes degrade specifically under duplicate-heavy key chains (see Microsoft Learn, "Indexes
    -- on Memory-Optimized Tables" -- Duplicate index key values) -- a nonclustered Bw-tree index tolerates
    -- that pattern far better.
    --
    -- Deliberately NOT indexed: LastRefreshedUtc, despite backing usp_AccountSession_ReapStale's own 6-minute
    -- sweep predicate. That column is bumped on every heartbeat poll (every ~2s, per shard, for every
    -- online account) -- indexing it would pay Bw-tree maintenance cost on that hot, frequent write for every
    -- online session just to speed up a sweep that runs once every 60s (AccountSessionReapHost, unsharded)
    -- over a table whose row count is bounded by concurrently-online-account count, which an in-memory hash
    -- full scan already services in well under a millisecond at realistic scale. This is the write-hot-vs-
    -- read-rare trade-off game.vw_GuildRosterCounts' own header already argues for indexed views; the
    -- CreatedAtUtc index on every runtime.*Relay table is the opposite case (write-once column) and is
    -- correctly indexed instead -- see e.g. GuildTribeBroadcastRelay.sql's own index comment.
    INDEX IX_AccountSessions_DeviceSignature NONCLUSTERED (AdapterIdentifier, LocalIp, RemoteIp)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
