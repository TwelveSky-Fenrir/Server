CREATE TABLE runtime.GuildBuffExpiryRelay
(
    RelayId       BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId TINYINT               NOT NULL,
    GuildId       INT                   NOT NULL,
    NewBuffTime   INT                   NOT NULL,
    CorrelationId UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc  DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_GuildBuffExpiryRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_GuildBuffExpiryRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_GuildBuffExpiryRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
