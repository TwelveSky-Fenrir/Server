-- Seeds world.RewardBundles/world.RewardBundleItems from the legacy `rewardinfo` table. LOW-CONFIDENCE:
-- rewardinfo has no PK and is the dump's only latin1-charset table -- signs of an abandoned feature.
IF
    NOT EXISTS (SELECT 1
                FROM world.RewardBundles)
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
