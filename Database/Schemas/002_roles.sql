-- No table/view grants exist anywhere -- every service EXECUTEs schema/object-scoped procedures only.
-- Login/user provisioning (mapping real logins to these roles) is a deployment-time concern, not versioned here.

-- Login service: full auth+runtime access, plus a narrow object-level slice of game (character-select
-- flow only) -- everything else in game (guilds, cash, world-entry persistence, ...) stays off-limits.
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

-- Game/world service: owns character data and world-entry flow, plus runtime; never auth. The only role
-- with world (reference data) access.
CREATE ROLE fenrir_game_role AUTHORIZATION dbo;
GO

GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::runtime TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::world TO fenrir_game_role;
GO

-- Gifts are read/claimed at character select (before world entry), so LoginServer needs these object-level
-- grants, same narrow posture as the character-select slice above.
GRANT
    EXECUTE
    ON
    OBJECT
    ::game.usp_Gift_GetPendingByAccount TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Gift_ClaimIntoVault TO fenrir_login_role;
GO
