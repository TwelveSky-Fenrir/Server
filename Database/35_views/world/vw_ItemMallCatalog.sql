-- Product-with-its-item shape, meant to be read from inside a retrieval procedure (security model:
-- views are never granted/called directly by app code -- see 60_permissions/001_roles.sql). An INNER
-- JOIN so a product whose ItemId is NULL (an empty/placeholder slot -- none are seeded, see
-- 70_seed/world/010_item_mall_products.sql) or that dangles on a since-removed world.Items row never
-- surfaces here. Deliberately projects only world.Items.ItemId, the one column every parallel domain
-- agrees on (the cross-domain PK contract) -- world.Items' other columns belong to a different
-- in-flight domain and are not assumed here; extend this view once that schema lands if a richer
-- catalog projection (item name, etc.) is wanted.
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
