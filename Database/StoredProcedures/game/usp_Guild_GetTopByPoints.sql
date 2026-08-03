CREATE PROCEDURE game.usp_Guild_GetTopByPoints @Count INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TOP (@Count) g.GuildId,
                        g.Name,
                        g.Points
    FROM game.Guilds g
    ORDER BY g.Points DESC, g.GuildId ASC;
END;
