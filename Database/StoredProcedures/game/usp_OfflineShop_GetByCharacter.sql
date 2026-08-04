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

    SELECT SlotIndex,
           ItemId,
           Quantity,
           Value,
           SerialNumber,
           Price,
           SocketData,
           SocketGem1,
           SocketGem2,
           SocketGem3
    FROM game.OfflineShopItems
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
