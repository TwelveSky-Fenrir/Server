CREATE TABLE runtime.SessionTickets
(
    CapabilityHash BINARY(32)       NOT NULL,
    AccountId      INT              NOT NULL,
    CharacterId    INT              NOT NULL,
    ShardId        TINYINT          NOT NULL,
    TargetMapId    SMALLINT         NOT NULL,
    ExpiresAtUtc   DATETIME2(3)     NOT NULL,
    SessionToken   UNIQUEIDENTIFIER NOT NULL,
    AccountGrade   SMALLINT         NOT NULL,
    SourceIpPrefix VARCHAR(45)      NOT NULL,
    CONSTRAINT CK_SessionTickets_Identifiers CHECK
        (AccountId > 0 AND CharacterId > 0 AND ShardId > 0 AND TargetMapId > 0),
    CONSTRAINT CK_SessionTickets_AccountGrade CHECK (AccountGrade >= 0),
    CONSTRAINT CK_SessionTickets_CapabilityHash CHECK
        (CapabilityHash <> 0x0000000000000000000000000000000000000000000000000000000000000000),
    CONSTRAINT CK_SessionTickets_SessionToken CHECK
        (SessionToken <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT CK_SessionTickets_SourceIpPrefix CHECK (SourceIpPrefix <> ''),
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (CapabilityHash)
        WITH (BUCKET_COUNT = 1024),
    INDEX UX_SessionTickets_AccountId UNIQUE NONCLUSTERED HASH (AccountId) WITH (BUCKET_COUNT = 1024),
    INDEX IX_SessionTickets_ExpiresAtUtc NONCLUSTERED (ExpiresAtUtc)
)
    WITH
(
    MEMORY_OPTIMIZED =
    ON,
    DURABILITY =
    SCHEMA_AND_DATA
);
