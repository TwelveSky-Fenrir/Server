-- Additive migration for game.GuildMembers (Server Logic chapter, Phase C/V7 Guilds & Tribes), same
-- rationale as Characters_progression.sql/Characters_social.sql: the migrator journal (admin.SchemaVersions)
-- forbids changing an applied script's content, so a NEW script is the sanctioned corrective path
-- (_manifest.txt header).
-- game.GuildMembers.sql's own header explicitly deferred this: "gMemberCall ... deliberately NOT carried
-- over ... add a nullable CallName column later if the wire contract for the guild-info panel turns out
-- to need it." It does: ZC_GUILD_WORK_RECV's GUILD_INFO.gMemberCall[50][5] (Core/Fenrir.Contracts/Packets/
-- Shared/GuildInfo.cs MemberCallNames) is populated from this on every tSort-2 roster query, and
-- CZ_GUILD_WORK_SEND tSort 10 (GUILD_MAKE_TITLE, doc 10 table §1) is the one write path for it.
-- MAX_CALL_NAME_LENGTH=5 (STRUCT.h) is 4 real chars + NUL, matching gMemberCall's own 4-char cosmetic
-- in-guild title semantics -- NVARCHAR(4), not 5 (Fenrir strings are never NUL-terminated in storage,
-- same convention as every other FixedString-backed column in this schema).
-- NOT NULL DEFAULT '': an unset title is legitimately "no title", the common case for a freshly-joined
-- member, exactly like game.GuildNotices.Text's own empty-string default.
ALTER TABLE game.GuildMembers
    ADD CallName NVARCHAR(4) NOT NULL CONSTRAINT DF_GuildMembers_CallName DEFAULT N'';
GO
