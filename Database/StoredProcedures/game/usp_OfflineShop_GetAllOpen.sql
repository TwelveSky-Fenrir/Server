-- database/50_procedures/game/usp_OfflineShop_GetAllOpen.sql
-- CZ_PSHOP_ITEM_INFO_SEND's proxy-shop half (Server/ts25zone/S04_MyWork02.cpp:6523-6558): one row per
-- for-sale slot across every currently open (ShopState=1) proxy shop, cluster-wide -- the shared database
-- is already the one store every shard's proxy shops persist through, so this deliberately carries no
-- @ZoneNumber filter (unlike the distinct, currently-unconsumed usp_OfflineShop_GetByZone).
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
           v.SocketData
    FROM game.vw_OfflineShopListing v
             JOIN game.Characters c ON c.CharacterId = v.CharacterId
    WHERE v.ShopState = 1
    ORDER BY v.CharacterId, v.SlotIndex;
END;
