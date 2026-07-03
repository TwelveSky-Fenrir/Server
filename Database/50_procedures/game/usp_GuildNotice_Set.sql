-- database/50_procedures/game/usp_GuildNotice_Set.sql
-- Contract: upsert one guild notice slot (replaces CSQLGuild.cpp's ADD/GET-with-fallback-to-ADD path;
-- legacy saves every 60 ticks + on every change, this proc is the "on every change" half).
-- Params:
--   @GuildId     INT          -- game.Guilds.GuildId
--   @NoticeIndex TINYINT      -- 0-3
--   @Text        NVARCHAR(50) -- new notice text (an empty string is a valid, deliberate "notice cleared")
-- Result set: none.
-- Idempotent: yes -- setting the same slot to the same text repeatedly is a no-op in effect (each call
-- still writes a fresh UpdatedAtUtc).
-- DELETE-then-INSERT, never MERGE/UPDATE (matches runtime.usp_SessionTicket_Create's idiom exactly): the
-- DELETE is a no-op the first time a given (GuildId, NoticeIndex) is ever set.
-- Natively compiled against game.GuildNotices (memory-optimized, SCHEMA_AND_DATA): this is the hottest
-- write path in the guild domain (legacy: every 60s + every edit), so it runs with zero locks/latches
-- and zero T-SQL interpretation.
CREATE PROCEDURE game.usp_GuildNotice_Set @GuildId     INT,
    @NoticeIndex TINYINT,
    @Text        NVARCHAR(50)
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM game.GuildNotices
WHERE GuildId = @GuildId
  AND NoticeIndex = @NoticeIndex;

INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
VALUES (@GuildId, @NoticeIndex, @Text, SYSUTCDATETIME());
END;
