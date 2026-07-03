-- Bundle-with-its-slots shape (LEFT JOIN: a bundle with zero populated slots would still legitimately
-- appear as one bundle row with all-NULL slot columns, even though no such row exists in the single
-- currently-seeded bundle). Kept to world.RewardBundles/world.RewardBundleItems only -- see
-- world.vw_ItemMallCatalog for why a join into world.Items itself is deliberately not repeated here.
CREATE VIEW world.vw_RewardBundleCatalog
AS
SELECT b.RewardBundleId,
       i.SlotIndex,
       i.ItemId
FROM world.RewardBundles b
         LEFT JOIN world.RewardBundleItems i ON i.RewardBundleId = b.RewardBundleId;
