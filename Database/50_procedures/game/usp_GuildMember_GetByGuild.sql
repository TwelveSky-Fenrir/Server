-- database/50_procedures/game/usp_GuildMember_GetByGuild.sql
-- Contract: roster for one guild (guild-info UI panel). Reads through game.vw_GuildRoster internally so
-- the result already carries the guild/character display names, not just ids.
-- Params:
--   @GuildId INT -- game.Guilds.GuildId
-- Result set (RS0), ordered by Role (master/sub-master first) then JoinedAtUtc:
--   1. GuildId       INT
--   2. GuildName     NVARCHAR(12)
--   3. CharacterId   INT
--   4. CharacterName NVARCHAR(13)
--   5. Role          TINYINT (0=member, 1=sub-master, 2=master)
--   6. CallName      NVARCHAR(4) -- [Phase C/V7 Guilds & Tribes] cosmetic in-guild title, see
--                      GuildMembers_callname.sql
--   7. JoinedAtUtc   DATETIME2(3)
-- Rows: 0..50 (MAX_GUILD_AVATAR_NUM legacy cap; not DB-enforced, an application-layer join-limit concern).
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @GuildId simply returns zero rows).
CREATE PROCEDURE game.usp_GuildMember_GetByGuild @GuildId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT GuildId,
       GuildName,
       CharacterId,
       CharacterName,
       Role,
       CallName,
       JoinedAtUtc
FROM game.vw_GuildRoster
WHERE GuildId = @GuildId
ORDER BY Role DESC, JoinedAtUtc;
END;
