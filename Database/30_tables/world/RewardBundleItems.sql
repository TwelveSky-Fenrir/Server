-- Normalizes legacy rewardinfo.rItem01..rItem07 (a fixed 7-slot array) into one row per populated
-- slot rather than 7 columns -- per the "don't transliterate fixed-size legacy arrays" rule for
-- anything with more than ~4 slots, even though the single observed legacy row happens to fill all 7.
-- SlotIndex is 1-based to match rItem01.."07 directly (no reordering).
-- ItemId is NULL, never 0, for the same reason as the other world.* tables in this batch -- kept
-- NULLable/FK'd defensively even though no populated slot in the current data needs it.
CREATE TABLE world.RewardBundleItems
(
    RewardBundleId INT     NOT NULL,
    SlotIndex      TINYINT NOT NULL,
    ItemId         INT NULL,
    CONSTRAINT PK_RewardBundleItems PRIMARY KEY CLUSTERED (RewardBundleId, SlotIndex),
    CONSTRAINT CK_RewardBundleItems_SlotIndex CHECK (SlotIndex BETWEEN 1 AND 7),
    CONSTRAINT FK_RewardBundleItems_RewardBundles FOREIGN KEY (RewardBundleId) REFERENCES world.RewardBundles (RewardBundleId),
    CONSTRAINT FK_RewardBundleItems_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
