CREATE PROCEDURE game.usp_TribeRoster_GetForTribePoint
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT c.Tribe                          AS TribeId,
           c.Level                          AS Level1,
           c.Level2,
           CAST(c.RebirthCount AS SMALLINT) AS RebirthCount
    FROM game.Characters c
    WHERE c.Level >= 145;
END;
