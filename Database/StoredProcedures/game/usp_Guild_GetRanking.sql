-- Cross-shard guild leaderboard read: adds MemberCount and a computed RankNo (ties share a rank) on top of
-- the raw GuildId/Name/Points usp_Guild_GetTopByPoints already returns, via game.vw_GuildRanking (see that
-- view's own header for why it must stay a plain, non-indexed view and can never legally become one).
-- Kept as its own procedure rather than altering the already-shipped usp_Guild_GetTopByPoints --
-- Database/_manifest.txt: an applied script is never edited in place, and this is additive, not a fix.
-- Same TOP(@Count) shape as usp_Guild_GetTopByPoints/usp_Guild_GetTopFourGuild: the caller decides how many
-- rows of the leaderboard it needs (a ranking-board UI page, a "top 10" broadcast, etc).
CREATE PROCEDURE game.usp_Guild_GetRanking @Count INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TOP (@Count) GuildId,
                        Name,
                        Points,
                        MemberCount,
                        RankNo
    FROM game.vw_GuildRanking
    ORDER BY RankNo, GuildId;
END;
