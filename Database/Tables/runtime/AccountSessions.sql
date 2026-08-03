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
    CONSTRAINT PK_AccountSessions PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_AccountSessions_DeviceSignature NONCLUSTERED (AdapterIdentifier, LocalIp, RemoteIp)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
