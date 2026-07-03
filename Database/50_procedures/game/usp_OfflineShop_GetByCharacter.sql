-- database/50_procedures/game/usp_OfflineShop_GetByCharacter.sql
-- Contract: @CharacterId -> 2 result sets (the shop-owner's own view, e.g. to re-open/edit their shop):
--   RS0: the game.OfflineShops row (0 or 1 rows)
--   RS1: one row per game.OfflineShopItems slot (0-25 rows), ordered by SlotIndex
-- Read-only, safe to retry.
CREATE PROCEDURE game.usp_OfflineShop_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT CharacterId,
       ZoneNumber,
       ShopState,
       ShopDate,
       Money,
       BigMoney,
       LocationX,
       LocationY,
       LocationZ,
       ShopName
FROM game.OfflineShops
WHERE CharacterId = @CharacterId;

SELECT SlotIndex, ItemId, Quantity, Value, SerialNumber, Price, SocketData
FROM game.OfflineShopItems
WHERE CharacterId = @CharacterId
ORDER BY SlotIndex;
END;
