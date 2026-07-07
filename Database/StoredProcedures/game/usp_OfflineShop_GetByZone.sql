-- database/50_procedures/game/usp_OfflineShop_GetByZone.sql
-- One row per for-sale item across every open shop in the zone, via game.vw_OfflineShopListing.
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
