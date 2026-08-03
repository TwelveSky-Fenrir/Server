CREATE PROCEDURE game.usp_Cash_GetBalance @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ISNULL((SELECT Balance
                   FROM game.AccountCash
                   WHERE AccountId = @AccountId), 0) AS Balance;
END;
