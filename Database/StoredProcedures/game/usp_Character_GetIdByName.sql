-- Name match uses game.Characters.Name's explicitly pinned SQL_Latin1_General_CP1_CI_AS (case-insensitive)
-- collation -- see that column's own definition/comment in Tables/game/Characters.sql.
CREATE PROCEDURE game.usp_Character_GetIdByName @Name NVARCHAR(13)
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT CharacterId
    FROM game.Characters
    WHERE Name = @Name;
END;
