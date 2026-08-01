-- Reads through game.vw_GuildRoster (already carries guild/character display names, not just ids).
CREATE PROCEDURE game.usp_GuildMember_GetByGuild @GuildId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT GuildId,
           GuildName,
           CharacterId,
           CharacterName,
           Role,
           CallName,
           JoinedAtUtc
    FROM game.vw_GuildRoster
    WHERE GuildId = @GuildId
    ORDER BY Role DESC, JoinedAtUtc;
END;
