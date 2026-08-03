CREATE TABLE runtime.GuildStateRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    Kind              TINYINT               NOT NULL,
    SourceShardId     TINYINT               NOT NULL,
    GuildId           INT                   NOT NULL,
    TargetCharacterId INT                   NULL,
    NewGuildId        INT                   NULL,
    GuildName         NVARCHAR(12)          NOT NULL,
    GuildRoleDb       TINYINT               NOT NULL,
    GuildCallName     NVARCHAR(4)           NOT NULL,
    BuffType          INT                   NOT NULL,
    BuffActive        BIT                   NOT NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_GuildStateRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_GuildStateRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_GuildStateRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
