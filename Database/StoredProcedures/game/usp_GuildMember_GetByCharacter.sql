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
