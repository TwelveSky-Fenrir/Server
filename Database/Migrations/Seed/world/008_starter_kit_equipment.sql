IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitEquipment)
    BEGIN
        INSERT INTO world.StarterKitEquipment (PreviousTribe, EquipSlot, ItemId, RawWeaponCode)
        VALUES
            (0, 0, 84671, NULL), 
            (0, 2, 84575, NULL), 
            (0, 3, 84623, NULL), 
            (0, 4, 84647, NULL), 
            (0, 5, 84599, NULL), 
            (0, 7, 84503, 5),    
            (0, 7, 84527, 6),    
            (0, 7, 84551, 7),    

            (1, 0, 85671, NULL), 
            (1, 2, 85575, NULL), 
            (1, 3, 85623, NULL), 
            (1, 4, 85647, NULL), 
            (1, 5, 85599, NULL), 
            (1, 7, 85503, 11),   
            (1, 7, 85527, 12),   
            (1, 7, 85551, 13),   

            (2, 0, 86671, NULL), 
            (2, 2, 86575, NULL), 
            (2, 3, 86623, NULL), 
            (2, 4, 86647, NULL), 
            (2, 5, 86599, NULL), 
            (2, 7, 86503, 17),   
            (2, 7, 86527, 18),   
            (2, 7, 86551, 19); 
    END;
