CREATE TABLE runtime.SocialCrossShardRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    Kind              TINYINT               NOT NULL,
    MessageType       TINYINT               NOT NULL,
    Accepted          BIT                   NULL,
    ReasonCode        TINYINT               NULL,
    SourceShardId     TINYINT               NOT NULL,
    SourceCharacterId INT                   NOT NULL,
    SourceAvatarName  NVARCHAR(13)          NOT NULL,
    TargetShardId     TINYINT               NOT NULL,
    TargetCharacterId INT                   NOT NULL,
    AskRelayId        BIGINT                NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_SocialCrossShardRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_SocialCrossShardRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_SocialCrossShardRelay_Target NONCLUSTERED (TargetShardId, RelayId),
    INDEX IX_SocialCrossShardRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
