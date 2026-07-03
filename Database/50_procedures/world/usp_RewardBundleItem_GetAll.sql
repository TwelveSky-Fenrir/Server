-- Contract: no parameters -> RS0 rows { RewardBundleId INT, SlotIndex TINYINT, ItemId INT NULL },
-- every populated slot across every bundle, ordered so all of one bundle's slots are contiguous --
-- pair with world.usp_RewardBundle_GetAll (see that proc's header for why this is two procs rather
-- than one multi-result-set proc).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_RewardBundleItem_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT RewardBundleId, SlotIndex, ItemId
FROM world.RewardBundleItems
ORDER BY RewardBundleId, SlotIndex;
END;
