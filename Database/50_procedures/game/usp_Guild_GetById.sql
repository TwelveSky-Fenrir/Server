-- database/50_procedures/game/usp_Guild_GetById.sql
-- Contract: ONE guild's own row (Phase C/V7 Guilds & Tribes -- GUILD_WORK tSort 2 "info/roster",
-- doc 10 §1: every successful GUILD_WORK response carries the requester's own guild's full GUILD_INFO,
-- not the whole realm's guild list usp_Guild_GetAll returns).
-- Params:
--   @GuildId INT
-- Result set (RS0), 0 or 1 row -- same column shape as usp_Guild_GetAll's own RS0:
--   1. GuildId           INT
--   2. Name              NVARCHAR(12)
--   3. Grade             INT
--   4. MasterCharacterId INT NULL
--   5. Points            INT
--   6. BuffType          INT
--   7. BuffState         INT
--   8. BuffTime          INT
--   9. BuffTimeForDiff   BIGINT
--   10. Logo             INT
--   11. CreatedAtUtc     DATETIME2(3)
--   12. MemberCount      INT (0 for a guild with no members yet -- plain COUNT(*), not COUNT_BIG(*))
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @GuildId simply returns zero rows).
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
