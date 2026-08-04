CREATE TABLE runtime.AccountSessions
(
    AccountId         INT              NOT NULL,
    ServerKind        TINYINT          NOT NULL,
    ShardId           TINYINT          NULL,
    SessionToken      UNIQUEIDENTIFIER NOT NULL,
    SessionState      TINYINT          NOT NULL,
    KickRequested     BIT              NOT NULL,
    ConnectedAtUtc    DATETIME2(3)     NOT NULL,
    LastRefreshedUtc  DATETIME2(3)     NOT NULL,
    AdapterIdentifier VARCHAR(128)     NULL,
    LocalIp           VARCHAR(45)      NULL,
    RemoteIp          VARCHAR(45)      NULL,
    CONSTRAINT CK_AccountSessions_AccountId CHECK (AccountId > 0),
    CONSTRAINT CK_AccountSessions_ServerKind CHECK (ServerKind IN (0, 1)),
    CONSTRAINT CK_AccountSessions_SessionState CHECK (SessionState IN (0, 1)),
    CONSTRAINT CK_AccountSessions_ServerShard CHECK
        ((ServerKind = 0 AND ShardId IS NULL) OR (ServerKind = 1 AND ShardId IS NOT NULL)),
    CONSTRAINT CK_AccountSessions_KickOwner CHECK (KickRequested = 0 OR ServerKind = 1),
    CONSTRAINT CK_AccountSessions_SessionToken CHECK
        (SessionToken <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT CK_AccountSessions_RefreshOrder CHECK (LastRefreshedUtc >= ConnectedAtUtc),
    CONSTRAINT PK_AccountSessions PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_AccountSessions_DeviceSignature NONCLUSTERED (AdapterIdentifier, LocalIp, RemoteIp)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
