-- LEFT JOIN: a bundle with zero populated slots still returns one row with all-NULL slot columns.
CREATE VIEW world.vw_RewardBundleCatalog
AS
SELECT b.RewardBundleId,
       i.SlotIndex,
       i.ItemId
FROM world.RewardBundles b
         LEFT JOIN world.RewardBundleItems i ON i.RewardBundleId = b.RewardBundleId;
