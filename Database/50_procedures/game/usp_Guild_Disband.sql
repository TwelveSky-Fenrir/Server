-- database/50_procedures/game/usp_Guild_Disband.sql
-- Contract: delete a guild and everything hanging off it (members, notices) in one transaction
-- (replaces ts25extra's guild-deletion tSort -- report 06 §3.1).
-- Params:
--   @GuildId INT
-- Result set: none.
-- Idempotent: no -- disbanding a guild that does not exist throws 50235 rather than silently no-op-ing,
-- so a caller cannot mistake "someone else already disbanded it / wrong id" for "disbanded just now"
-- (same non-idempotence rationale as usp_Gift_Claim).
-- game.GuildNotices is memory-optimized: inside an explicit (cross-container) transaction it MUST be
-- accessed WITH (SNAPSHOT) -- the database does not set MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT, so the
-- hint is mandatory, not defensive. Children first, then the guild row.
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Guild_Disband @GuildId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

DELETE
FROM game.GuildNotices WITH (SNAPSHOT)
WHERE GuildId = @GuildId;

DELETE
FROM game.GuildMembers
WHERE GuildId = @GuildId;

DELETE
FROM game.Guilds
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1; -- XACT_ABORT rolls the whole transaction back

COMMIT TRANSACTION;
END;
