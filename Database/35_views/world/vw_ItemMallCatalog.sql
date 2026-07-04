-- INNER JOIN excludes products with a NULL/dangling ItemId.
CREATE VIEW world.vw_ItemMallCatalog
AS
SELECT p.ItemMallProductId,
       p.ProductType,
       i.ItemId,
       p.Quantity,
       p.Cost,
       p.IsActive
FROM world.ItemMallProducts p
         INNER JOIN world.Items i ON i.ItemId = p.ItemId;
