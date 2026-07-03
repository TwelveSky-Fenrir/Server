-- Contract: no parameters -> RS0 rows { ItemMallProductId INT, ProductType TINYINT, ItemId INT,
--           Quantity INT, Cost INT, IsActive BIT (always 1) }, only products currently on sale -- the
-- shape the live cash-shop feature actually needs, as opposed to world.usp_ItemMallProduct_GetAll's
-- full admin dump. Reads world.vw_ItemMallCatalog internally (security model: views are an internal
-- T-SQL readability tool a procedure's body may use -- see 60_permissions/001_roles.sql), so a
-- product referencing a since-removed world.Items row is silently excluded rather than surfacing a
-- dangling ItemId to the game client.
-- Read-only, safe to retry.
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
