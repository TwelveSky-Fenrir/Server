-- Shop-item-with-display-name shape, meant to be read from inside a retrieval procedure (security model:
-- views are never granted/called directly by app code -- see 60_permissions/001_roles.sql). Backs the
-- "others browse/buy" half of the offline-vending-stall feature (game.usp_OfflineShop_GetByZone): a browse
-- UI needs the item's Name, not just its ItemId, to render a listing.
-- INNER JOIN to game.OfflineShopItems (not LEFT): an empty sale slot has ItemId=NULL and is not a listing
-- worth showing to a browsing player, so slots with nothing for sale are correctly excluded entirely rather
-- than appearing as a blank row.
CREATE VIEW game.vw_OfflineShopListing
AS
SELECT s.CharacterId,
       s.ZoneNumber,
       s.ShopState,
       s.LocationX,
       s.LocationY,
       s.LocationZ,
       i.SlotIndex,
       i.ItemId,
       it.Name AS ItemName,
       i.Quantity,
       i.Value,
       i.SerialNumber,
       i.Price,
       i.SocketData
FROM game.OfflineShops s
         JOIN game.OfflineShopItems i ON i.CharacterId = s.CharacterId
         JOIN world.Items it ON it.ItemId = i.ItemId;
