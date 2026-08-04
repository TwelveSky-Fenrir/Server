IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitEquipment)
    BEGIN
        INSERT INTO world.StarterKitEquipment (PreviousTribe, EquipSlot, ItemId, RawWeaponCode)
        VALUES (0, 2, 8, NULL),
               (0, 3, 9, NULL),
               (0, 5, 10, NULL),
               (0, 7, 5, 5),
               (0, 7, 6, 6),
               (0, 7, 7, 7),

               (1, 2, 14, NULL),
               (1, 3, 15, NULL),
               (1, 5, 16, NULL),
               (1, 7, 11, 11),
               (1, 7, 12, 12),
               (1, 7, 13, 13),

               (2, 2, 20, NULL),
               (2, 3, 21, NULL),
               (2, 5, 22, NULL),
               (2, 7, 17, 17),
               (2, 7, 18, 18),
               (2, 7, 19, 19);
    END;
