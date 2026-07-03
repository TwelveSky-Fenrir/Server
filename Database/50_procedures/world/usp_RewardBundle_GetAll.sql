-- Contract: no parameters -> RS0 rows { RewardBundleId INT }, every bundle id -- pair with
-- world.usp_RewardBundleItem_GetAll to reconstruct each bundle's slot list at boot (one proc per
-- table, no ad hoc multi-result-set shape, matching every other world.* retrieval proc in this repo).
-- Currently exactly 1 row; low-confidence/possibly-stale legacy data -- see
-- 30_tables/world/RewardBundles.sql.
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_RewardBundle_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT RewardBundleId
FROM world.RewardBundles
ORDER BY RewardBundleId;
END;
