-- database/50_procedures/game/usp_Guild_GetAll.sql
-- Contract: full guild list (admin/tooling + any future GameServer guild-directory warm start). Unlike
-- world.* reference data this is live player state, so a real per-request client path should prefer the
-- narrower usp_GuildMember_GetByGuild/usp_GuildNotice_GetByGuild -- this proc exists so game.Guilds has
-- at least one retrieval path at all, since no SELECT grant on the table itself ever exists
-- (Database/60_permissions/001_roles.sql "no table rights" model).
-- Uses game.vw_GuildRosterCounts internally (a view is never queried directly by app code) to attach a
-- live headcount without a second round trip.
-- Params: none.
-- Result set (RS0), ordered by GuildId:
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
--   12. MemberCount      INT (0 for a guild with no members yet -- [CORRIGÉ-REVUE, Phase C/V7] plain
--                         COUNT(*) in game.vw_GuildRosterCounts returns INT, not BIGINT -- verified
--                         against a real SQL Server container after game.GuildSummaryDto's own
--                         BIGINT-typed field threw InvalidCastException the first time this ran for real)
-- Idempotent: yes (read-only).
-- Errors: none.
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
