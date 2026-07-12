-- database/50_procedures/game/usp_Guild_AddFourGuildPoints.sql
-- Four-guild live-scoring point accrual (behavior contract C17, Part B / side effect B1;
-- Server/ts25extra/S04_MyWork02.cpp:1263-1264 + Server/ts25extra/S08_MyDB.cpp:1146-1148, UpdateGuildPoint's
-- direct increment UPDATE). Credits the killer's guild +@Delta guild points (@Delta is always +1 per kill at
-- the sole live legacy call site, Server/ts25zone/S07_MyGame03.cpp:3098-3100) when the zone is running in the
-- zone-049-type event mode.
--
-- Distinct from game.usp_Guild_AdjustPoints, which THROWs 50234 on an unknown guild: this accrual is
-- best-effort and fire-and-forget by contract -- a guild not present in the table (e.g. the killer's guild
-- was disbanded mid-event) simply matches no row and the point is silently lost, never surfaced as an error
-- to the killing client (Server/ts25extra/S08_MyDB.cpp:1148). So there is NO admin.ErrorCatalog entry and NO
-- THROW here. Keyed by GuildId rather than by the legacy op22 guild-name payload because Fenrir callers
-- already resolve the killer's guild membership by CharacterId (PlayerRuntimeState.GuildId) before reaching
-- here -- same key-choice rationale as usp_Guild_AdjustPoints.
--
-- Writes game.Guilds.Points (legacy gPoint), the same counter game.usp_Guild_GetTopByPoints / the RvR guild
-- ranking board reads -- exactly as the legacy four-guild event and the guild ranking share gPoint. The
-- non-negative guard is defense-in-depth against a stray negative delta; the sole live caller only ever adds.
CREATE PROCEDURE game.usp_Guild_AddFourGuildPoints @GuildId INT,
                                                   @Delta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET Points       = Points + @Delta,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND Points + @Delta >= 0;
END;
