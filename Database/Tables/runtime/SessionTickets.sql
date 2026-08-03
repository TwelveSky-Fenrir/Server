CREATE TABLE runtime.SessionTickets
(
    AccountId      INT              NOT NULL,
    CharacterId    INT              NOT NULL,
    ShardId        TINYINT          NOT NULL,
    TargetMapId    SMALLINT         NOT NULL,
    ExpiresAtUtc   DATETIME2(3)     NOT NULL,
    SessionToken   UNIQUEIDENTIFIER NOT NULL,
    AccountGrade   SMALLINT         NOT NULL,
    SourceIpPrefix VARCHAR(45)      NULL,
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_SessionTickets_ExpiresAtUtc NONCLUSTERED (ExpiresAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
