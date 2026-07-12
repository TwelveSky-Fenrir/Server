-- INNER JOIN throughout is deliberate: a GuildMembers row without a matching Characters/Guilds row would
-- indicate corrupt data, not a legitimate "empty roster" case (that's covered by vw_GuildRosterCounts).
-- Not an indexed-view candidate, same reasoning as vw_GuildRosterCounts: game.GuildMembers is mutated on
-- every join/leave (usp_GuildMember_Add/usp_GuildMember_Remove) -- frequently-mutated player state, not
-- reference data.
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
