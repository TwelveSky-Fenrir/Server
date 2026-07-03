-- database/50_procedures/game/usp_Guild_AdjustPoints.sql
-- Contract: atomically add (positive) or subtract (negative) guild points (legacy gPoint,
-- UpdateGuildPoint opcode 22 + the four-guild ranking that reads it -- report 06 §3.6); rejects, never
-- clamps, an adjustment that would drive Points negative (same no-silent-clamp philosophy as
-- usp_TribeBank_Withdraw / usp_Character_AdjustMoney -- points are spendable value).
-- Params: @GuildId INT, @Delta INT (may be negative).
-- Result set: none.
-- Idempotent: no (each successful call moves Points by @Delta).
-- Single guarded UPDATE: atomic under concurrent adjusters of the same guild. A missing GuildId is
-- indistinguishable from an insufficient balance here (both = 0 rows updated) -- acceptable, both are
-- "the adjustment did not happen" to the caller, and the error text says so.
-- Errors:
--   THROW 50234 -- guild not found, or the adjustment would drive Points negative
--                  (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Guild_AdjustPoints @GuildId INT,
    @Delta   INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Guilds
SET Points = Points + @Delta
WHERE GuildId = @GuildId
  AND Points + @Delta >= 0;

IF
@@ROWCOUNT = 0
        THROW 50234, N'Unknown guild or insufficient guild points for this adjustment.', 1;
END;
