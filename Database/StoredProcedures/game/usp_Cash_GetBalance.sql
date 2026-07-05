-- No AccountCash row means balance 0 (rows are only minted by the first credit) -- callers never see NULL.
CREATE PROCEDURE game.usp_Cash_GetBalance @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT ISNULL((SELECT Balance
               FROM game.AccountCash
               WHERE AccountId = @AccountId), 0) AS Balance;
END;
