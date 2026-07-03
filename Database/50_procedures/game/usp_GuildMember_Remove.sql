-- database/50_procedures/game/usp_GuildMember_Remove.sql
-- Contract: remove one character from a guild (leave AND kick collapse to the same row deletion --
-- report 06 §3.1; who initiated it is the application layer's business, plus its own notifications).
-- Params:
--   @GuildId     INT
--   @CharacterId INT
-- Result set: none.
-- Idempotent: yes -- removing a character that is not (or no longer) a member is a silent no-op, same
-- §12.3 contract as usp_Character_Delete: leave/kick races (member quits while the master kicks them)
-- must both succeed.
-- Removing the MASTER is the application layer's responsibility to prevent (transfer with
-- usp_Guild_SetMaster or disband instead); this proc stays a dumb row deletion on purpose -- guild
-- succession policy is gameplay, not storage.
-- Errors: none.
CREATE PROCEDURE game.usp_GuildMember_Remove @GuildId     INT,
    @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.GuildMembers
WHERE GuildId = @GuildId
  AND CharacterId = @CharacterId;
END;
