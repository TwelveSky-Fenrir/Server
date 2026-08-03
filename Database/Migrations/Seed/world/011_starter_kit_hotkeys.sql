IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitHotkeys)
    BEGIN
        INSERT INTO world.StarterKitHotkeys (PreviousTribe, Page, KeyIndex, Sort, Value1, Value2)
        VALUES (0, 0, 0, 1, 1, 1),
               (0, 0, 1, 34, 999, 3),
               (0, 0, 2, 34, 999, 3),
               (1, 0, 0, 20, 1, 1),
               (1, 0, 1, 34, 999, 3),
               (1, 0, 2, 34, 999, 3),
               (2, 0, 0, 39, 1, 1),
               (2, 0, 1, 34, 999, 3),
               (2, 0, 2, 34, 999, 3);
    END;
