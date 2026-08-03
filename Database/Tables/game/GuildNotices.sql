CREATE TABLE game.GuildNotices
(
    GuildId      INT          NOT NULL,
    NoticeIndex  TINYINT      NOT NULL,
    Text         NVARCHAR(50) NOT NULL
        CONSTRAINT DF_GuildNotices_Text DEFAULT N'',
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_GuildNotices_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GuildNotices PRIMARY KEY NONCLUSTERED (GuildId, NoticeIndex),
    CONSTRAINT CK_GuildNotices_NoticeIndex CHECK (NoticeIndex BETWEEN 0 AND 3)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
