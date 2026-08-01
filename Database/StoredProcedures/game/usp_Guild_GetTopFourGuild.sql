-- database/50_procedures/game/usp_Guild_GetTopFourGuild.sql
-- Four-guild live-scoring leaderboard recompute (behavior contract C17, Part B / side effect B2;
-- Server/ts25extra/S08_MyDB.cpp:1109-1140, LoadFourGuild). Returns the top @Count guilds with a STRICTLY
-- POSITIVE point total, ordered by points descending -- the positive-only filter is the difference from
-- game.usp_Guild_GetTopByPoints (which returns the top N unconditionally, zeros included, for the general RvR
-- ranking board). FourGuildScoringService reads this on its recompute cadence (~10s in the legacy) and copies
-- the standings into the shared display/broadcast state.
-- GuildId ASC breaks ties deterministically (stable leaderboard tests); same RS0 shape (GuildId, Name,
-- Points) as usp_Guild_GetTopByPoints, so it reuses the existing GuildRankingRowDto.
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
