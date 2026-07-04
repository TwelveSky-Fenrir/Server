-- database/50_procedures/game/usp_GuildMember_GetByCharacter.sql
-- Contract: this character's OWN guild membership row (if any) -- the character-keyed twin of
-- usp_GuildMember_GetByGuild (guild-keyed). Added for Phase C/V6 Social: EnterWorldHandler loads this
-- once at world entry (same "load once, cache on PlayerRuntimeState" posture as
-- admin.usp_Mute_GetActiveForCharacter, report 06 §1.7) so guild chat/announcement routing never queries
-- SQL per chat message.
-- Params:
--   @CharacterId INT
-- Result set (RS0), 0 or 1 row (UQ_GuildMembers_CharacterId: a character belongs to at most one guild):
--   1. GuildId   INT
--   2. GuildName NVARCHAR(12)
--   3. Role      TINYINT (game.GuildMembers role enum: 0 member, 1 sub-master, 2 master -- NOTE this is
--                the DB-side enum, which is the INVERSE of the raw legacy wire's aGuildRole encoding
--                (0=master/1=sub-master/2=member, verified S04_MyWork02.cpp:10148/10621-10659 "Guide
--                Transfer Leader"); callers translate via GuildRoleCodec, never compare this raw value
--                against a wire constant directly.
--   4. CallName  NVARCHAR(4) -- [Phase C/V7 Guilds & Tribes] cosmetic in-guild title, see
--                GuildMembers_callname.sql
-- Idempotent: yes (read-only).
-- Errors: none (an unknown/guildless @CharacterId simply returns zero rows).
CREATE PROCEDURE game.usp_GuildMember_GetByCharacter @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT m.GuildId, g.Name AS GuildName, m.Role, m.CallName
    FROM game.GuildMembers AS m
    JOIN game.Guilds AS g ON g.GuildId = m.GuildId
    WHERE m.CharacterId = @CharacterId;
END;
