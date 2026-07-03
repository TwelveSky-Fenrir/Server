-- Contract: no parameters -> RS0 rows { ItemMallProductId INT, ProductType TINYINT, ItemId INT NULL,
--           Quantity INT, Cost INT, IsActive BIT }, every row (active and inactive) -- the boot-time
-- full-table load for GameServer's in-memory cash-shop cache. Also the shape GM/admin tooling needs
-- to see and re-enable a currently-inactive product, which world.usp_ItemMallProduct_GetActive alone
-- cannot show.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_ItemMallProduct_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT ItemMallProductId, ProductType, ItemId, Quantity, Cost, IsActive
FROM world.ItemMallProducts
ORDER BY ItemMallProductId;
END;
