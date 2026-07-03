-- database/50_procedures/game/usp_GuildMember_SetRole.sql
-- Contract: set one member's role (promotion/demotion to/from sub-master -- report 06 §3.1; the legacy
-- packs this as an ASCII digit inside the gMember blob).
-- Params:
--   @GuildId     INT
--   @CharacterId INT
--   @Role        TINYINT -- 0 member, 1 sub-master (CK_GuildMembers_Role backstops validity; role 2 =
--                           master is usp_Guild_SetMaster's job, which keeps Guilds.MasterCharacterId
--                           consistent -- callers should not set 2 through here)
-- Result set: none.
-- Idempotent: yes for the same (member, role) pair; a missing member throws rather than silently
-- no-op-ing -- a role change that lands on nobody is a caller bug worth surfacing.
-- Errors:
--   THROW 50233 -- (GuildId, CharacterId) is not a member of that guild (admin.ErrorCatalog, 502xx).
CREATE PROCEDURE game.usp_GuildMember_SetRole @GuildId     INT,
    @CharacterId INT,
    @Role        TINYINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.GuildMembers
SET Role = @Role
WHERE GuildId = @GuildId
  AND CharacterId = @CharacterId;

IF
@@ROWCOUNT = 0
        THROW 50233, N'Character is not a member of this guild.', 1;
END;
