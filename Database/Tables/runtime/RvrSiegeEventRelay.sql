CREATE TABLE runtime.RvrSiegeEventRelay
(
    RelayId       BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId TINYINT               NOT NULL,
    Sort          INT                   NOT NULL,
    Data          VARBINARY(130)        NOT NULL,
    CorrelationId UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc  DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_RvrSiegeEventRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_RvrSiegeEventRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_RvrSiegeEventRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
