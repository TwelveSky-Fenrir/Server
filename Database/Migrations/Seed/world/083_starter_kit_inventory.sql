-- Same function's unconditional aInventory[0][0..3] block -- identical regardless of tribe/template.
IF
NOT EXISTS (SELECT 1 FROM world.StarterKitInventory)
BEGIN
INSERT INTO world.StarterKitInventory (SlotIndex, ItemId, Quantity)
VALUES (0, 1026, 999), -- Return Scroll
       (1, 1109, 999), -- Teleport Scroll
       (2, 1224, 999), -- Dungeon Teleport Scroll
       (3, 1001, 10); -- Pet Food
END;
