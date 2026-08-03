CREATE PROCEDURE game.usp_Guild_GetById @GuildId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT g.GuildId,
           g.Name,
           g.Grade,
           g.MasterCharacterId,
           g.Points,
           g.BuffType,
           g.BuffState,
           g.BuffTime,
           g.BuffTimeForDiff,
           g.Logo,
           g.CreatedAtUtc,
           COALESCE(c.MemberCount, 0) AS MemberCount
    FROM game.Guilds g
             LEFT JOIN game.vw_GuildRosterCounts c ON c.GuildId = g.GuildId
    WHERE g.GuildId = @GuildId;
END;
