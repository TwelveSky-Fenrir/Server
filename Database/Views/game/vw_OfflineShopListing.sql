SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE VIEW game.vw_OfflineShopListing
    WITH SCHEMABINDING
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
         INNER JOIN game.OfflineShopItems i ON i.CharacterId = s.CharacterId
         INNER JOIN world.Items it ON it.ItemId = i.ItemId;
GO

CREATE UNIQUE CLUSTERED INDEX IX_vw_OfflineShopListing_CharacterId_SlotIndex
    ON game.vw_OfflineShopListing (CharacterId, SlotIndex);
GO
