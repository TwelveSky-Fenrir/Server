CREATE TABLE runtime.PartyResyncRelay
(
    RelayId           BIGINT IDENTITY (1,1) NOT NULL,
    Sort              TINYINT               NOT NULL,
    SourceShardId     TINYINT               NOT NULL,
    SourceCharacterId INT                   NOT NULL,
    PartyName         NVARCHAR(13)          NOT NULL,
    AvatarName        NVARCHAR(13)          NOT NULL,
    MemberId1         INT                   NOT NULL,
    MemberName1       NVARCHAR(13)          NOT NULL,
    MemberId2         INT                   NOT NULL,
    MemberName2       NVARCHAR(13)          NOT NULL,
    MemberId3         INT                   NOT NULL,
    MemberName3       NVARCHAR(13)          NOT NULL,
    MemberId4         INT                   NOT NULL,
    MemberName4       NVARCHAR(13)          NOT NULL,
    MemberId5         INT                   NOT NULL,
    MemberName5       NVARCHAR(13)          NOT NULL,
    CorrelationId     UNIQUEIDENTIFIER      NOT NULL,
    CreatedAtUtc      DATETIME2(3)          NOT NULL,
    CONSTRAINT PK_PartyResyncRelay PRIMARY KEY NONCLUSTERED (RelayId),
    CONSTRAINT UQ_PartyResyncRelay_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_PartyResyncRelay_CreatedAtUtc NONCLUSTERED (CreatedAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
