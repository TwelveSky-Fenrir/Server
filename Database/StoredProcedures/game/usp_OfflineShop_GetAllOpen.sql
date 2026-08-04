CREATE PROCEDURE game.usp_OfflineShop_GetAllOpen
AS
BEGIN
    SET NOCOUNT ON;

    SELECT v.CharacterId,
           c.Name AS AvatarName,
           v.SlotIndex,
           v.ItemId,
           v.Quantity,
           v.Value,
           v.SerialNumber,
           v.Price,
           v.SocketData,
           v.SocketGem1,
           v.SocketGem2,
           v.SocketGem3
    FROM game.vw_OfflineShopListing v WITH (NOEXPAND)
             JOIN game.Characters c ON c.CharacterId = v.CharacterId
    WHERE v.ShopState = 1
    ORDER BY v.CharacterId, v.SlotIndex;
END;
