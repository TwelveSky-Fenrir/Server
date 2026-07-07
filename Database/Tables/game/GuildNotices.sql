-- Replaces GUILD_NOTICE_INFO.DAT; MEMORY_OPTIMIZED/SCHEMA_AND_DATA since this is a hot, frequent write
-- (saved every 60 ticks + on every add/remove), unlike the rest of this domain.
-- PK is a NONCLUSTERED range index, not HASH: usp_GuildNotice_GetByGuild prefix-scans all 4 rows for one
-- GuildId, which a hash index can't serve efficiently.
-- No FK on GuildId: In-Memory OLTP tables can't FK to disk-based game.Guilds; enforced by the application
-- (usp_GuildNotice_Set is the sole writer).
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
