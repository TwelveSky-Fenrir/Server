-- Seeds world.RewardBundles/world.RewardBundleItems from the legacy MySQL dump's `rewardinfo` table.
-- LOW-CONFIDENCE / POSSIBLY-STALE (per the prior analysis pass): rewardinfo is the only latin1-charset
-- table in the entire dump and has no PRIMARY KEY of its own, both signs of a neglected/abandoned
-- feature. It also contains exactly 1 row (rIndex=1, rItem01..rItem07 all = 12) -- seeded verbatim
-- below, but this should not be read as a vetted, actively-used reward table.
IF
NOT EXISTS (SELECT 1 FROM world.RewardBundles)
BEGIN
INSERT INTO world.RewardBundles (RewardBundleId)
VALUES (1);

INSERT INTO world.RewardBundleItems (RewardBundleId, SlotIndex, ItemId)
VALUES (1, 1, 12),
       (1, 2, 12),
       (1, 3, 12),
       (1, 4, 12),
       (1, 5, 12),
       (1, 6, 12),
       (1, 7, 12);
END;
