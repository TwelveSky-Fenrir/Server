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
