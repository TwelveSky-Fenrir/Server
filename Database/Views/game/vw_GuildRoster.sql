-- INNER JOIN throughout is deliberate: a GuildMembers row without a matching Characters/Guilds row would
-- indicate corrupt data, not a legitimate "empty roster" case (that's covered by vw_GuildRosterCounts).
CREATE VIEW game.vw_GuildRoster
AS
SELECT g.GuildId,
       g.Name AS GuildName,
       m.CharacterId,
       c.Name AS CharacterName,
       m.Role,
       m.CallName,
       m.JoinedAtUtc
FROM game.Guilds g
         JOIN game.GuildMembers m ON m.GuildId = g.GuildId
         JOIN game.Characters c ON c.CharacterId = m.CharacterId;
