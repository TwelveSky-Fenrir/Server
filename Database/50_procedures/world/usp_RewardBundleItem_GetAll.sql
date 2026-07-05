CREATE PROCEDURE world.usp_RewardBundleItem_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT RewardBundleId, SlotIndex, ItemId
FROM world.RewardBundleItems
ORDER BY RewardBundleId, SlotIndex;
END;
