CREATE PROCEDURE game.usp_Character_GetByAccount @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT CharacterId,
           Slot,
           Name,
           Tribe,
           Gender,
           HeadType,
           FaceType,
           Level
    FROM game.Characters
    WHERE AccountId = @AccountId
    ORDER BY Slot;
END;
