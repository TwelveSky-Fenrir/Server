CREATE TABLE runtime.GuildTribeBroadcastRelay
(
    RelayId          BIGINT IDENTITY (1,1) NOT NULL,
    Kind             TINYINT               NOT NULL,
    SourceShardId    TINYINT               NOT NULL,
    SourceCharacterId INT                  NULL,
    SystemCause      TINYINT               NULL,
    GuildId          INT                   NULL,
    Tribe            TINYINT               NULL,
    RoleField        TINYINT               NOT NULL,
    AvatarName       NVARCHAR(13)          NOT NULL,
    Content          NVARCHAR(61)          NOT NULL,
    HasItemLink      BIT                   NOT NULL,
    ItemLinkIndex    INT                   NULL,
    ItemLinkActivity INT                   NULL,
    ItemLinkValue    INT                   NULL,
    ItemLinkSocket0  INT                   NULL,
    ItemLinkSocket1  INT                   NULL,
    ItemLinkSocket2  INT                   NULL,
    CorrelationId    UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc     DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_GuildTribeBroadcastRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_GuildTribeBroadcastRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    CONSTRAINT CK_GuildTribeBroadcastRelay_CorrelationId CHECK
        (CorrelationId <> '00000000-0000-0000-0000-000000000000'),
    INDEX IX_GuildTribeBroadcastRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
