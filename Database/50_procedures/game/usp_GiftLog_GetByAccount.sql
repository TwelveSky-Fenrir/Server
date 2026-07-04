-- Added so game.GiftLog (write-only otherwise) can be queried under this schema's procs-only security model.
CREATE PROCEDURE game.usp_GiftLog_GetByAccount @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT GiftLogId, ProductId, Quantity, Value, Status, CreatedAtUtc
FROM game.GiftLog
WHERE AccountId = @AccountId
ORDER BY CreatedAtUtc DESC;
END;
