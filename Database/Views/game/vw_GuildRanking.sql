CREATE VIEW game.vw_GuildRanking
AS
SELECT g.GuildId,
       g.Name,
       g.Points,
       COALESCE(c.MemberCount, 0)                        AS MemberCount,
       CAST(RANK() OVER (ORDER BY g.Points DESC) AS INT) AS RankNo
FROM game.Guilds g
         LEFT JOIN game.vw_GuildRosterCounts c ON c.GuildId = g.GuildId;
