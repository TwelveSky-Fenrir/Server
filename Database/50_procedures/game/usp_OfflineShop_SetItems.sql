-- database/50_procedures/game/usp_OfflineShop_SetItems.sql
-- Whole-list replace (DELETE-then-INSERT, never MERGE): the client submits its entire shop layout as one
-- batch, never slot-by-slot.
CREATE PROCEDURE game.usp_OfflineShop_SetItems @CharacterId INT,
    @Items       game.tvp_OfflineShopItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.OfflineShopItems
WHERE CharacterId = @CharacterId;

INSERT INTO game.OfflineShopItems (CharacterId, SlotIndex, ItemId, Quantity, Value, SerialNumber, Price, SocketData)
SELECT @CharacterId,
       SlotIndex,
       ItemId,
       Quantity,
       Value,
       SerialNumber,
       Price,
       SocketData
FROM @Items;
END;
