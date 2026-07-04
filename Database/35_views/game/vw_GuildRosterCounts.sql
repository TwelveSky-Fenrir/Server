-- Deliberately a plain (not indexed) view: an indexed view would force strict ANSI SET options on
-- every future write to game.GuildMembers, which is frequently-mutated player state, not reference data.
CREATE VIEW game.vw_GuildRosterCounts
AS
SELECT GuildId,
       COUNT(*) AS MemberCount
FROM game.GuildMembers
GROUP BY GuildId;
