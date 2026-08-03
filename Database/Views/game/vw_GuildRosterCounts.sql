CREATE VIEW game.vw_GuildRosterCounts
AS
SELECT GuildId,
       COUNT(*) AS MemberCount
FROM game.GuildMembers
GROUP BY GuildId;
