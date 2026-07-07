-- MemberCount is INT (not BIGINT): game.vw_GuildRosterCounts' COUNT(*) returns INT, and the DTO field
-- must match or it throws InvalidCastException.
CREATE PROCEDURE game.usp_Guild_GetAll
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
    ORDER BY g.GuildId;
END;
