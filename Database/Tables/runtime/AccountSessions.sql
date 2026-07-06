-- One row per account with a currently-tracked session anywhere in the cluster. SCHEMA_ONLY like
-- runtime.SessionTickets/GameServerDirectory: a crash wiping this table just means every account reads
-- as "no active session", which is the safe direction to fail toward for a duplicate-login guard.
CREATE TABLE runtime.AccountSessions
(
    AccountId        INT              NOT NULL,
    ServerKind       TINYINT          NOT NULL, -- 0 = Login, 1 = Game
    ShardId          TINYINT          NULL,     -- meaningful only when ServerKind = 1
    SessionToken     UNIQUEIDENTIFIER NOT NULL, -- minted once at Login-claim time, carried through runtime.SessionTickets
    SessionState     TINYINT          NOT NULL, -- 0 = Active, 1 = TearingDown
    KickRequested    BIT              NOT NULL,
    ConnectedAtUtc   DATETIME2(3)     NOT NULL,
    LastRefreshedUtc DATETIME2(3)     NOT NULL, -- bumped by the owning process's own periodic poll; 6-min sweep reads this
    CONSTRAINT PK_AccountSessions PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
