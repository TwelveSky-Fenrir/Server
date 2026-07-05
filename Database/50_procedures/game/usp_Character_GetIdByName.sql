-- Name match uses UQ_Characters_Name's default (case-insensitive) collation.
CREATE PROCEDURE game.usp_Character_GetIdByName @Name NVARCHAR(13)
AS
BEGIN
    SET
NOCOUNT ON;

SELECT CharacterId
FROM game.Characters
WHERE Name = @Name;
END;
