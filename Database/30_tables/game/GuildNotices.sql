-- Replaces GUILD_NOTICE_INFO.DAT (CSQLGuild.cpp ProcessForGuildNotice): a flat file of GUILD_NOTICE_DB
-- records { char tGuildName[13]; char tGuildNotice[4][51]; }, LOAD at boot / SAVE every 60 ticks + on
-- every ADD/REMOVE (§ task brief) -- i.e. this is a hot, frequent write, unlike the rest of this domain.
-- MEMORY_OPTIMIZED / SCHEMA_AND_DATA (not SCHEMA_ONLY like runtime.SessionTickets): a notice is real
-- persisted guild content a player wrote, not disposable session state -- it must survive a restart the
-- same way the legacy .DAT file survives a process bounce.
-- NoticeIndex is the legacy tGuildNotice[4] slot (0-3, GUILD_NOTICE_V2's 4-notice-per-guild model, which
-- superseded the older 4-separate-column gNotice01..04 still visible in the guildinfo dump) -- a real
-- fixed 4-slot array, not a sparse one, so plain numbered columns would also have been defensible, but a
-- child table keeps the "set one slot" write path a single-row upsert instead of a whole-row update.
-- PK is a NONCLUSTERED range index, not HASH: usp_GuildNotice_GetByGuild seeks all 4 rows for one
-- GuildId (a prefix scan on the leading PK column), which a hash index -- built for full-key equality
-- only -- cannot serve efficiently; a Bw-tree range index serves both the exact-match upsert path and
-- the by-GuildId read path from the same index (architecture reference's memory-optimized index guidance).
-- NOTE -- no FK on GuildId to game.Guilds: SQL Server only allows a FOREIGN KEY on a memory-optimized
-- table when the referenced table is ALSO memory-optimized (In-Memory OLTP unsupported-constructs
-- reference), and game.Guilds is a regular disk-based table (guild metadata is not a hot-write path).
-- Making Guilds memory-optimized just to satisfy this FK would drag game.Characters into the same
-- requirement transitively (Guilds.MasterCharacterId -> Characters) for no real benefit. Referential
-- correctness is instead guaranteed by construction: the only place a GuildId ever originates is
-- game.Guilds.GuildId (a never-reused IDENTITY), and the application layer is the sole writer of this
-- table via usp_GuildNotice_Set.
CREATE TABLE game.GuildNotices
(
    GuildId      INT     NOT NULL,
    NoticeIndex  TINYINT NOT NULL,
    Text         NVARCHAR(50) NOT NULL CONSTRAINT DF_GuildNotices_Text DEFAULT N'',
    UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GuildNotices_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GuildNotices PRIMARY KEY NONCLUSTERED (GuildId, NoticeIndex),
    CONSTRAINT CK_GuildNotices_NoticeIndex CHECK (NoticeIndex BETWEEN 0 AND 3)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
