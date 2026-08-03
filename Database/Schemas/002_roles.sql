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

CREATE ROLE fenrir_game_role AUTHORIZATION dbo;
GO

GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::runtime TO fenrir_game_role;
GO

GRANT EXECUTE ON SCHEMA::world TO fenrir_game_role;
GO

GRANT
    EXECUTE
    ON
    OBJECT
    ::game.usp_Gift_GetPendingByAccount TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Gift_ClaimIntoVault TO fenrir_login_role;
GO
