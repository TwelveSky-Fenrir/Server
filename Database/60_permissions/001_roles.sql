-- Database roles only (architecture reference §12.3): "no table rights, the attack surface = the exact
-- list of procedures". Every service connects as a login mapped to one of these roles and can EXECUTE
-- schema-scoped procedures only -- no SELECT/INSERT/UPDATE/DELETE grant exists anywhere on a table or
-- view, so a compromised connection is limited to whatever the procedure contracts already allow.
--
-- Login/user provisioning (CREATE LOGIN/CREATE USER with real credentials, then
-- `ALTER ROLE fenrir_login_role ADD MEMBER <user>;` etc.) is deliberately NOT in this script: that is a
-- deployment-time concern (Phase 7 Aspire / operations, secrets-managed), not something to version in
-- clear text in Git. This script only creates the roles and grants EXECUTE at the schema level so the
-- surface is fixed and auditable independently of which logins end up mapped to it.

-- Login service (Login server process): authenticates accounts and issues/relays session tickets.
-- Needs auth (usp_Account_*) and runtime (usp_SessionTicket_*, usp_GameServer_*) in full, plus a
-- deliberately NARROW, object-level (not schema-level) slice of game: the character-select flow
-- (CL_CREATE_AVATAR_SEND2/CL_DELETE_AVATAR_SEND/CL_CHANGE_AVATAR_NAME_SEND handlers,
-- CharacterRepository/CharacterRenameRepository) runs on this process and genuinely needs these four
-- procs -- everything else in game (guilds, cash, tribes, world-entry persistence, ...) stays off-limits,
-- preserving "a compromised LoginServer connection cannot reach gameplay state" even though it must reach
-- the character roster.
CREATE ROLE fenrir_login_role AUTHORIZATION dbo;
GO

GRANT EXECUTE ON SCHEMA::auth TO fenrir_login_role;
GO

GRANT EXECUTE ON SCHEMA::runtime TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Character_GetByAccount TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Character_Create TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Character_Delete TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Character_Rename TO fenrir_login_role;
GO

-- Game/world service (zone server process): owns character data and world-entry flow, plus the same
-- runtime directory/ticket procedures to hand off/validate a session -- never auth (no password access).
-- Also the only role with `world` access (read-mostly reference data: items/skills/monsters/npcs/quests/
-- zone placement/spawn regions) -- LoginServer never touches gameplay reference data.
CREATE ROLE fenrir_game_role AUTHORIZATION dbo;
GO

GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::runtime TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::world TO fenrir_game_role;
GO
