CREATE PROCEDURE game.usp_AccountVault_Get @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT AccountId, Money, Money2, UpdatedAtUtc, BigMoney, Revision
    FROM game.AccountVault
    WHERE AccountId = @AccountId;

    SELECT SlotIndex,
           ItemId,
           Quantity,
           Value,
           SerialNumber,
           SocketData,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate
    FROM game.AccountVaultItems
    WHERE AccountId = @AccountId
    ORDER BY SlotIndex;
END;
