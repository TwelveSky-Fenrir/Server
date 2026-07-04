-- "Guild with its child rows" shape: a member row joined out to its guild's name and its character's
-- name, the natural read shape for a guild-roster UI panel. INNER JOIN throughout is deliberate (not
-- LEFT JOIN): a GuildMembers row without a matching Characters row, or referencing a nonexistent Guilds
-- row, would both indicate corrupt data, not a legitimate "empty roster" case worth surfacing here --
-- the zero-members case is already covered by that guild's absence from game.vw_GuildRosterCounts.
-- Consumed internally by game.usp_GuildMember_GetByGuild -- never granted/queried directly by app code.
-- [Phase C/V7 Guilds & Tribes] m.CallName added: GuildMembers_callname.sql's own header explains why the
-- column exists; this view is its one read path (via usp_GuildMember_GetByGuild), same "view is never
-- queried directly by app code" posture as every other column here.
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
