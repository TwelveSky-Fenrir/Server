-- INNER JOIN excludes products with a NULL/dangling ItemId.
--
-- Schema-bound with a unique clustered index on ItemMallProductId -- this repo's first indexed
-- (materialized) view. Costs nothing at write time: world.ItemMallProducts/world.Items are populated
-- exclusively by Migrations/Seed/world/*.sql -- no INSERT/UPDATE/DELETE against either table exists anywhere
-- under Database/StoredProcedures/.
--
-- Currently a zero-benefit, zero-cost asset, not a hot-path optimization: the one procedure that reads this
-- view, world.usp_ItemMallProduct_GetActive, has zero call sites anywhere under Infrastructure/Fenrir.Data --
-- IWorldDataRepository.GetItemMallProductsAsync calls the unfiltered world.usp_ItemMallProduct_GetAll
-- instead, and CashCatalogBuilder reproduces this view's own IsActive/ItemId-join filter entirely in memory
-- from that boot-loaded snapshot. The index is kept as a free, ready-to-use asset for whenever a real
-- repository consumer is wired to world.usp_ItemMallProduct_GetActive -- do not describe it as "hot" without
-- citing an actual call site.
--
-- game.vw_HeroRankingCurrent was evaluated for the same treatment and declined: game.HeroRankings is written
-- per-kill by game.usp_HeroRanking_AddPoints across every shard and already carries a covering
-- IX_HeroRankings_Period_Points index, so schema-binding it would charge every kill-reward grant for a
-- benefit an occasional leaderboard read doesn't need.
--
-- ARITHABORT caveat: a wrong ARITHABORT setting on a READING connection doesn't break correctness -- the
-- optimizer just silently falls back to the base tables, forfeiting this index's read-time benefit. But per
-- Microsoft Learn ("Create indexed views" required-SET-options table + "SET ARITHABORT (Transact-SQL)"):
-- ARITHABORT OFF on a WRITING connection makes any INSERT/UPDATE/DELETE against this view's base tables
-- (world.ItemMallProducts/world.Items) fail outright with an error -- not a silent degradation. world's base
-- tables are seed-only (never DML'd at runtime), so this specific view has no live write path exposed to
-- that failure today, but do not assume that stays true if a runtime write path is ever added here. Verify
-- with `SELECT CASE WHEN (64 & @@OPTIONS) = 64 THEN 'ON' ELSE 'OFF' END AS Arithabort;` from a session opened
-- the same way CaeriusNet opens one before relying on this index mattering.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE VIEW world.vw_ItemMallCatalog
    WITH SCHEMABINDING
AS
SELECT p.ItemMallProductId,
       p.ProductType,
       i.ItemId,
       p.Quantity,
       p.Cost,
       p.IsActive
FROM world.ItemMallProducts p
         INNER JOIN world.Items i ON i.ItemId = p.ItemId;
GO

CREATE UNIQUE CLUSTERED INDEX IX_vw_ItemMallCatalog_ItemMallProductId
    ON world.vw_ItemMallCatalog (ItemMallProductId);
GO
