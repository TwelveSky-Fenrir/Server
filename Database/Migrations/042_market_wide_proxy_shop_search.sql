-- market-wide-proxy-shop-search (Major): PSHOP_ITEM_INFO_SEND is a unified market aggregator that merges
-- every currently-open PROXY/deputy shop across the whole cluster with every open PERSONAL shop on the
-- local zone instance only (Server/ts25zone/S04_MyWork02.cpp:6523-6558 proxy, :6559-6585 personal;
-- ServerDocs/12_ts25zone/05_MyWork02_PartieB.md §3.4). Fenrir's SearchShopListingsService previously only
-- ever searched live personal shops and never queried the proxy/offline-shop store at all -- since a
-- personal shop's listings vanish the instant its owner disconnects (unlike the persisted proxy/offline
-- shops, arguably the primary real-world marketplace use case), the "search the market" feature could never
-- surface the majority of real-world listings a player would expect to find.
--
-- This adds the read that closes that gap: every for-sale slot across every currently open (ShopState=1)
-- proxy shop, cluster-wide -- not scoped to any one zone/shard, since the shared database is already the
-- single store every proxy shop persists through (the Fenrir-sharded equivalent of legacy's cross-instance
-- shared-memory proxy-shop table game.vw_OfflineShopListing already assembles the shop+item join; this adds
-- the ShopState=1 filter and the game.Characters join for the seller's own display name (AvatarName), which
-- OfflineShops itself does not persist (see Tables/game/OfflineShops.sql's own header on ShopName being a
-- dead-code column). Deliberately carries no @ZoneNumber parameter, unlike the distinct, currently-unconsumed
-- usp_OfflineShop_GetByZone.
--
-- Brand-new stored procedure (first appearance, never previously shipped) -- CREATE PROCEDURE, not CREATE OR
-- ALTER; no already-applied script is edited by this migration.
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
GO
