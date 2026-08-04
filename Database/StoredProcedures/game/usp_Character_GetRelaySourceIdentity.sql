CREATE PROCEDURE game.usp_Character_GetRelaySourceIdentity @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Name,
           c.Tribe,
           a.AccountGrade
    FROM game.Characters AS c
    INNER JOIN auth.Accounts AS a ON a.AccountId = c.AccountId
    WHERE c.CharacterId = @CharacterId;
END;
