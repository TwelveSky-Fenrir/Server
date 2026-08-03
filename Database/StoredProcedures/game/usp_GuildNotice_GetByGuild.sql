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
