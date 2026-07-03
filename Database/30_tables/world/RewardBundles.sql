-- Legacy MySQL `rewardinfo` (nxtserver.sql): the only latin1-charset table in the entire dump and the
-- only one with no PRIMARY KEY/index of its own -- a sign of neglect already flagged by the prior
-- analysis pass. RewardBundleId is the legacy `rIndex`, stored as-given (not IDENTITY): even though
-- the legacy table itself has no formal key, rIndex is still the value gameplay code would look a
-- bundle up by, so it's treated like the other meaningful legacy ids in this batch, not a surrogate.
-- Exactly 1 row exists in the dump -- low-confidence/possibly-stale data per the prior analysis, seeded
-- as-is (see 70_seed/world/012_reward_bundles.sql) but not silently presented as solid.
-- Item slots live in the child table world.RewardBundleItems (normalized from rItem01..rItem07).
CREATE TABLE world.RewardBundles
(
    RewardBundleId INT NOT NULL,
    CONSTRAINT PK_RewardBundles PRIMARY KEY CLUSTERED (RewardBundleId)
);
