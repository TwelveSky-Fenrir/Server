CREATE PROCEDURE game.usp_AccountVault_Get @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT AccountId, Money, Money2, UpdatedAtUtc, BigMoney
    FROM game.AccountVault
    WHERE AccountId = @AccountId;

    SELECT SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
    FROM game.AccountVaultItems
    WHERE AccountId = @AccountId
    ORDER BY SlotIndex;
END;
