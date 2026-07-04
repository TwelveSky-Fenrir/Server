-- Legacy MySQL `rewardinfo`.rIndex; RewardBundleId stored as-given (not IDENTITY). Items normalized into world.RewardBundleItems.
CREATE TABLE world.RewardBundles
(
    RewardBundleId INT NOT NULL,
    CONSTRAINT PK_RewardBundles PRIMARY KEY CLUSTERED (RewardBundleId)
);
