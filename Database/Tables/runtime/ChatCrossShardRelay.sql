CREATE TABLE runtime.ChatCrossShardRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    SourceShardId     TINYINT               NOT NULL,
    SourceCharacterId INT                   NOT NULL,
    SourceAvatarName  NVARCHAR(13)          NOT NULL,
    TargetShardId     TINYINT               NOT NULL,
    TargetCharacterId INT                   NOT NULL,
    TargetAvatarName  NVARCHAR(13)          NOT NULL,
    Content           NVARCHAR(61)          NOT NULL,
    SenderAuthType    TINYINT               NOT NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_ChatCrossShardRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_ChatCrossShardRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_ChatCrossShardRelay_Target NONCLUSTERED (TargetShardId, RelayId),
    INDEX IX_ChatCrossShardRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
