CREATE TABLE runtime.CharacterShardLocation
(
    CharacterId INT          NOT NULL,
    ShardId     TINYINT      NOT NULL,
    MapId       SMALLINT     NOT NULL,
    AvatarName  NVARCHAR(13) NOT NULL,
    Tribe       TINYINT      NOT NULL,
    LastSeenUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_CharacterShardLocation PRIMARY KEY NONCLUSTERED HASH (CharacterId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_CharacterShardLocation_AvatarName NONCLUSTERED HASH (AvatarName) WITH (BUCKET_COUNT = 1024)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
