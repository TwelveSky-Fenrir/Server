IF
    NOT EXISTS (SELECT 1
                FROM world.RewardBundles)
    BEGIN
        INSERT INTO world.RewardBundles (RewardBundleId)
        VALUES (1);

        INSERT INTO world.RewardBundleItems (RewardBundleId, SlotIndex, ItemId)
        VALUES (1, 1, 8406), 
               (1, 2, 8420), 
               (1, 3, 8411), 
               (1, 4, 613),  
               (1, 5, 8414), 
               (1, 6, 8406), 
               (1, 7, 8413); 
    END;
