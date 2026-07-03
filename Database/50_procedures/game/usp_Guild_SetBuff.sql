-- database/50_procedures/game/usp_Guild_SetBuff.sql
-- Contract: set the guild-wide buff block (legacy gBuffType/gBuffState/gBuffTime/gBuffTimeForDiff,
-- USE_GUILD_BUFF -- report 06 §2.10: activation = this update + an in-process notification to connected
-- members; the center rebroadcast is dead).
-- Params:
--   @GuildId         INT
--   @BuffType        INT
--   @BuffState       INT
--   @BuffTime        INT
--   @BuffTimeForDiff BIGINT -- bigint(19) in the legacy dump, mirrors game.Guilds.BuffTimeForDiff
-- Result set: none.
-- Idempotent: yes (setting the same block twice is a no-op in effect).
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Guild_SetBuff @GuildId         INT,
    @BuffType        INT,
    @BuffState       INT,
    @BuffTime        INT,
    @BuffTimeForDiff BIGINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Guilds
SET BuffType        = @BuffType,
    BuffState       = @BuffState,
    BuffTime        = @BuffTime,
    BuffTimeForDiff = @BuffTimeForDiff
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
