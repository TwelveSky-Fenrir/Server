CREATE PROCEDURE world.usp_ItemMallProduct_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ItemMallProductId, ProductType, ItemId, Quantity, Cost, IsActive
FROM world.ItemMallProducts
ORDER BY ItemMallProductId;
END;
