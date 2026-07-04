-- ItemId NULL means empty slot (translate legacy 0 -> NULL before calling).
CREATE PROCEDURE game.usp_AccountVault_SetItems @AccountId INT,
    @Items     game.tvp_AccountVaultItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.AccountVaultItems
WHERE AccountId = @AccountId;

INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
SELECT @AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
FROM @Items;
END;
