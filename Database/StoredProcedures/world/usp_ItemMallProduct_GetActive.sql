CREATE PROCEDURE world.usp_ItemMallProduct_GetActive
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ItemMallProductId, ProductType, ItemId, Quantity, Cost, IsActive
    FROM world.vw_ItemMallCatalog
    WHERE IsActive = 1
    ORDER BY ItemMallProductId;
END;
