-- INNER JOIN to game.OfflineShopItems excludes empty sale slots (ItemId NULL) rather than showing blank rows.
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
