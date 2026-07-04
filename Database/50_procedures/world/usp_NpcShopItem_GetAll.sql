CREATE PROCEDURE world.usp_NpcShopItem_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId, ShopPage, SlotIndex, ItemId
FROM world.NpcShopItems
ORDER BY NpcId, ShopPage, SlotIndex;
END;
