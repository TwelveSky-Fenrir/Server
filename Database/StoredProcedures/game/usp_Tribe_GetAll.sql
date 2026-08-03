CREATE PROCEDURE game.usp_Tribe_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT t.TribeId,
           t.MasterCharacterId,
           w.Points
    FROM game.Tribes AS t
             LEFT JOIN game.WorldStateTribes AS w ON w.TribeId = t.TribeId
    ORDER BY t.TribeId;
END;
