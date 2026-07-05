-- Role is the DB-side enum (0 member/1 sub-master/2 master), the INVERSE of the raw legacy wire's
-- aGuildRole encoding; callers must translate via GuildRoleCodec, never compare directly.
CREATE PROCEDURE game.usp_GuildMember_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT m.GuildId, g.Name AS GuildName, m.Role, m.CallName
FROM game.GuildMembers AS m
         JOIN game.Guilds AS g ON g.GuildId = m.GuildId
WHERE m.CharacterId = @CharacterId;
END;
