-- database/50_procedures/game/usp_OfflineShop_GetByZone.sql
-- Contract: @ZoneNumber -> RS0 { CharacterId, ZoneNumber, ShopState, LocationX, LocationY, LocationZ,
--           SlotIndex, ItemId, ItemName, Quantity, Value, SerialNumber, Price, SocketData }, one row per
--           for-sale item across every shop currently open in that zone, ordered by CharacterId, SlotIndex.
-- Backs the "others browse/buy" half of the offline-vending-stall feature (see game.OfflineShops):
-- a player walking through a zone needs to see what every open shop there is selling.
-- Read-only, safe to retry. Reads game.vw_OfflineShopListing (already joined to world.Items for ItemName).
CREATE PROCEDURE game.usp_OfflineShop_GetByZone @ZoneNumber SMALLINT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT CharacterId,
       ZoneNumber,
       ShopState,
       LocationX,
       LocationY,
       LocationZ,
       SlotIndex,
       ItemId,
       ItemName,
       Quantity,
       Value,
       SerialNumber,
       Price,
       SocketData
FROM game.vw_OfflineShopListing
WHERE ZoneNumber = @ZoneNumber
ORDER BY CharacterId, SlotIndex;
END;
