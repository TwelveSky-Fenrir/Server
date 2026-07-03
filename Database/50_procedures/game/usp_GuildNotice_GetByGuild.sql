-- database/50_procedures/game/usp_GuildNotice_GetByGuild.sql
-- Contract: the 4 MOTD/notice slots for a guild (replaces CSQLGuild.cpp's GetGuildNotice load path).
-- Plain (non-native) proc: reads are not the hot path here, only game.usp_GuildNotice_Set's writes are
-- -- see game.GuildNotices for why the table itself is still memory-optimized.
-- Params:
--   @GuildId INT -- game.Guilds.GuildId
-- Result set (RS0), ordered by NoticeIndex, one row per slot that has ever been set (0..4 rows -- a
-- never-set slot simply has no row, see game.GuildNotices):
--   1. GuildId      INT
--   2. NoticeIndex  TINYINT (0-3)
--   3. Text         NVARCHAR(50)
--   4. UpdatedAtUtc DATETIME2(3)
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @GuildId simply returns zero rows).
CREATE PROCEDURE game.usp_GuildNotice_GetByGuild @GuildId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT GuildId,
       NoticeIndex,
       Text,
       UpdatedAtUtc
FROM game.GuildNotices
WHERE GuildId = @GuildId
ORDER BY NoticeIndex;
END;
