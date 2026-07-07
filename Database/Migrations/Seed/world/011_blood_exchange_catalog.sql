-- Seeds world.BloodExchangeCatalog from the legacy `bloodinfo` table; only slots 1, 2, and outlier
-- slot 100000 carry real data, the other 48 of 51 rows are zeroed filler and are dropped.
IF
    NOT EXISTS (SELECT 1
                FROM world.BloodExchangeCatalog)
    BEGIN
        INSERT INTO world.BloodExchangeCatalog (BloodExchangeSlot, ItemId, Cost, Quantity)
        VALUES (1, 12, 5, 0),
               (2, 1019, 9999, 1),
               (100000, 6, 0, 0);
    END;
