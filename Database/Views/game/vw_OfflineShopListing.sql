-- INNER JOIN to game.OfflineShopItems excludes empty sale slots (ItemId NULL) rather than showing blank rows.
--
-- Schema-bound with a unique clustered index on (CharacterId, SlotIndex) -- Fenrir's second indexed
-- (materialized) view, alongside world.vw_ItemMallCatalog. Its one live consumer,
-- game.usp_OfflineShop_GetAllOpen, fires on every player's shop-search packet cluster-wide with deliberately
-- no @ZoneNumber/shard filter -- proxy shops persist through the one shared database every shard reads, so a
-- per-shard filter/cache would be incoherent the instant a different shard's seller opens/closes/sells a
-- shop (see that procedure's own header comment). game.OfflineShops/game.OfflineShopItems are written only
-- on discrete player actions -- open/close/purchase/retrieve, via
-- usp_OfflineShop_OpenAndReplaceContainers/usp_OfflineShop_ExecutePurchase/
-- usp_OfflineShop_RetrieveItemAndReplaceContainer/usp_OfflineShop_SetState -- never per-tick, so the
-- indexed-view write tax lands at a moderate, discrete-event rate against what can be a very frequent
-- cross-shard read (every marketplace search, from every shard).
--
-- Mechanical checklist verified against Microsoft Learn ("Create indexed views"): WITH SCHEMABINDING; all
-- three base tables referenced in two-part schema.table form (game.OfflineShops / game.OfflineShopItems /
-- world.Items); every projected column is a plain, deterministic column reference; INNER JOIN only, no outer
-- join/subquery/aggregate/TOP/ORDER BY/DISTINCT/UNION in the view body (the ShopState=1 filter and the
-- ORDER BY both already live in usp_OfflineShop_GetAllOpen, not here, so nothing had to move to make this
-- eligible). (CharacterId, SlotIndex) is game.OfflineShopItems' own PRIMARY KEY and both columns are NOT NULL
-- there, so it's a free, valid unique-clustered-index key: the join to game.OfflineShops (at most one row
-- per CharacterId, its own PK) and world.Items (at most one row per ItemId, its own PK) can only narrow or
-- drop rows, never fan OfflineShopItems' own (CharacterId, SlotIndex) grain out.
--
-- ARITHABORT caveat (same open item as world.vw_ItemMallCatalog's own header comment): a wrong ARITHABORT
-- setting on a READING connection doesn't break correctness -- the optimizer just silently falls back to the
-- base tables, forfeiting this index's read-time benefit. But per Microsoft Learn ("Create indexed views"
-- required-SET-options table + "SET ARITHABORT (Transact-SQL)"): ARITHABORT OFF on a WRITING connection makes
-- any INSERT/UPDATE/DELETE against this view's base tables fail outright with an error, not a silent
-- degradation -- and UNLIKE world.vw_ItemMallCatalog, game.OfflineShops/game.OfflineShopItems ARE written at
-- runtime by live gameplay procedures (usp_OfflineShop_OpenAndReplaceContainers, usp_OfflineShop_
-- ExecutePurchase, usp_OfflineShop_SetState, usp_OfflineShop_RetrieveItemAndReplaceContainer,
-- usp_OfflineShop_WithdrawMoney), so a mis-set ARITHABORT on any of those connections would surface as a hard
-- write failure for this feature, not merely a slower read. Verify with
-- `SELECT CASE WHEN (64 & @@OPTIONS) = 64 THEN 'ON' ELSE 'OFF' END AS Arithabort;` from a session opened the
-- same way CaeriusNet opens one, before relying on either indexed view mattering; consider
-- `EXECUTE sp_configure 'user options', <mask including 64>; RECONFIGURE;` server-wide once that's confirmed.
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
