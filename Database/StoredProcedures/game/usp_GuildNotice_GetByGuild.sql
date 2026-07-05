-- Plain (non-native) proc: only usp_GuildNotice_Set's writes are hot-path; see game.GuildNotices
-- for why the table itself is still memory-optimized.
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
