-- database/50_procedures/game/usp_GuildMember_SetCallName.sql
-- Contract: set one member's cosmetic in-guild title (GUILD_MAKE_TITLE, doc 10 §1 tSort 10 -- the legacy
-- packs this as gMemberCall[slot] inside the gMember blob; see GuildMembers_callname.sql for why it's a
-- real column here instead).
-- Params:
--   @GuildId     INT
--   @CharacterId INT
--   @CallName    NVARCHAR(4) -- an empty string is a valid, deliberate "title cleared" (same convention
--                as usp_GuildNotice_Set's own @Text)
-- Result set: none.
-- Idempotent: yes for the same (member, title) pair; a missing member throws rather than silently
-- no-op-ing -- same rationale as usp_GuildMember_SetRole (a title change that lands on nobody is a
-- caller bug worth surfacing).
-- Errors:
--   THROW 50233 -- (GuildId, CharacterId) is not a member of that guild (admin.ErrorCatalog, 502xx --
--                  reused from usp_GuildMember_SetRole's identical "not a member" case).
CREATE PROCEDURE game.usp_GuildMember_SetCallName @GuildId     INT,
    @CharacterId INT,
    @CallName    NVARCHAR(4)
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.GuildMembers
SET CallName = @CallName
WHERE GuildId = @GuildId
  AND CharacterId = @CharacterId;

IF
@@ROWCOUNT = 0
        THROW 50233, N'Character is not a member of this guild.', 1;
END;
