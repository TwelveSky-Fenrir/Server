-- "Guild with its child rows" shape: a member row joined out to its guild's name and its character's
-- name, the natural read shape for a guild-roster UI panel. INNER JOIN throughout is deliberate (not
-- LEFT JOIN): a GuildMembers row without a matching Characters row, or referencing a nonexistent Guilds
-- row, would both indicate corrupt data, not a legitimate "empty roster" case worth surfacing here --
-- the zero-members case is already covered by that guild's absence from game.vw_GuildRosterCounts.
-- Consumed internally by game.usp_GuildMember_GetByGuild -- never granted/queried directly by app code.
CREATE VIEW game.vw_GuildRoster
AS
SELECT g.GuildId,
       g.Name AS GuildName,
       m.CharacterId,
       c.Name AS CharacterName,
       m.Role,
       m.JoinedAtUtc
FROM game.Guilds g
         JOIN game.GuildMembers m ON m.GuildId = g.GuildId
         JOIN game.Characters c ON c.CharacterId = m.CharacterId;
