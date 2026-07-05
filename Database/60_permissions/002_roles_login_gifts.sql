-- Gifts are read/claimed at character select (before world entry), so LoginServer needs these object-level
-- grants, same narrow posture as 001_roles.sql's character-select slice.
GRANT
EXECUTE
ON
OBJECT
::game.usp_Gift_GetPendingByAccount TO fenrir_login_role;
GO

GRANT EXECUTE ON OBJECT::game.usp_Gift_ClaimIntoVault TO fenrir_login_role;
GO
