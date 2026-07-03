-- database/50_procedures/game/usp_Guild_SetMaster.sql
-- Contract: transfer guild leadership (legacy gChangeLeader flow, report 06 §3.1) -- keeps the THREE
-- places leadership lives consistent in one transaction: Guilds.MasterCharacterId, the old master's
-- member Role, the new master's member Role.
-- Params:
--   @GuildId              INT
--   @NewMasterCharacterId INT -- must already be a member of @GuildId (add first, then transfer)
-- Result set: none.
-- Idempotent: yes -- re-transferring to the current master leaves the same end state.
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
--   THROW 50233 -- the new master is not a member of this guild.
CREATE PROCEDURE game.usp_Guild_SetMaster @GuildId              INT,
    @NewMasterCharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    -- [CORRIGÉ-REVUE] Checked BEFORE the membership check: without a guild-exists check of its own, a
    -- missing @GuildId always surfaced as 50233 ("not a member" -- true, but not the actual cause) and the
    -- transaction's own `@@ROWCOUNT = 0` guard below could never fire (no membership row => 50233 already
    -- thrown), leaving 50235 dead code despite being documented/cataloged as reachable.
    IF
NOT EXISTS (SELECT 1 FROM game.Guilds WHERE GuildId = @GuildId)
        THROW 50235, N'Guild not found.', 1;

    IF
NOT EXISTS (SELECT 1
                   FROM game.GuildMembers
                   WHERE GuildId = @GuildId
                     AND CharacterId = @NewMasterCharacterId)
        THROW 50233, N'Character is not a member of this guild.', 1;

BEGIN
TRANSACTION;

UPDATE game.GuildMembers
SET Role = 0 -- demote whoever currently holds master
WHERE GuildId = @GuildId
  AND Role = 2
  AND CharacterId <> @NewMasterCharacterId;

UPDATE game.GuildMembers
SET Role = 2
WHERE GuildId = @GuildId
  AND CharacterId = @NewMasterCharacterId;

UPDATE game.Guilds
SET MasterCharacterId = @NewMasterCharacterId
WHERE GuildId = @GuildId;

COMMIT TRANSACTION;
END;
