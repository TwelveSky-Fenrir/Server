CREATE PROCEDURE game.usp_CharacterLogoutState_GetByAccount @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ls.CharacterId,
           ls.LastZone,
           ls.PosX,
           ls.PosY,
           ls.PosZ,
           ls.Life,
           ls.Mana
    FROM game.CharacterLogoutState ls
             INNER JOIN game.Characters c ON c.CharacterId = ls.CharacterId
    WHERE c.AccountId = @AccountId;
END;
