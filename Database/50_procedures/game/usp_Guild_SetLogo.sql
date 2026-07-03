-- database/50_procedures/game/usp_Guild_SetLogo.sql
-- Contract: set the guild logo (legacy gLogo, USE_GUILD_LOGO tSort 1001 -- report 06 §3.1).
-- Params:
--   @GuildId INT
--   @Logo    INT -- legacy logo id/mark number, stored verbatim
-- Result set: none.
-- Idempotent: yes.
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Guild_SetLogo @GuildId INT,
    @Logo    INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Guilds
SET Logo = @Logo
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
