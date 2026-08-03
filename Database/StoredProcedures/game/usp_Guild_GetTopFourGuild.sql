CREATE PROCEDURE game.usp_Guild_GetTopFourGuild @Count INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TOP (@Count) g.GuildId,
                        g.Name,
                        g.Points
    FROM game.Guilds g
    WHERE g.Points > 0
    ORDER BY g.Points DESC, g.GuildId ASC;
END;
