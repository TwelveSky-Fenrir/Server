-- RS0 (AccountVault) is empty until usp_AccountVault_EnsureInitialized has run for this account.
CREATE PROCEDURE game.usp_AccountVault_Get @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT AccountId, Money, Money2, UpdatedAtUtc
FROM game.AccountVault
WHERE AccountId = @AccountId;

SELECT SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
FROM game.AccountVaultItems
WHERE AccountId = @AccountId
ORDER BY SlotIndex;
END;
