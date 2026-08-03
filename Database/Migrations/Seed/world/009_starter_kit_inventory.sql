IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitInventory)
    BEGIN
        INSERT INTO world.StarterKitInventory (SlotIndex, ItemId, Quantity)
        VALUES (0, 1026, 999), 
               (1, 1109, 999), 
               (2, 1224, 999), 
               (3, 1001, 10); 
    END;
