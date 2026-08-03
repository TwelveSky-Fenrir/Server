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
