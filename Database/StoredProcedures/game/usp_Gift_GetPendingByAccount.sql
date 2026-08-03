CREATE PROCEDURE game.usp_Gift_GetPendingByAccount @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT GiftId, ProductId, Quantity, Value, CreatedAtUtc
    FROM game.Gifts
    WHERE AccountId = @AccountId
      AND Status = 0
    ORDER BY CreatedAtUtc;
END;
