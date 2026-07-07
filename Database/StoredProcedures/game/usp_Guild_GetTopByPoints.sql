-- RvR guild ranking board (WorldInfo.GuildName3/GuildScore): the top N guilds by game.Guilds.Points.
-- GuildId ASC breaks ties deterministically (matters for stable ranking-board tests, not for gameplay).
-- Nothing populates Points yet -- game.usp_Guild_AdjustPoints has no caller in this codebase -- so every
-- guild ranks 0 until whichever RvR scoring feature calls it.
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
