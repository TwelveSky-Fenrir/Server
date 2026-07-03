-- database/50_procedures/game/usp_Guild_SetGrade.sql
-- Contract: set the guild grade (legacy gGrade, guild level-up flow -- report 06 §3.1).
-- Params:
--   @GuildId INT
--   @Grade   INT -- stored verbatim (grade thresholds/costs are gameplay rules, Phase C)
-- Result set: none.
-- Idempotent: yes.
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Guild_SetGrade @GuildId INT,
    @Grade   INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Guilds
SET Grade = @Grade
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
