-- Seeds world.BloodExchangeCatalog from the legacy MySQL dump's `bloodinfo` table (51 rows total).
-- Filtering decision: 48 of those 51 rows (slots 3-50) are pure ItemID=0/Cost=0/Quantity=0 filler
-- with no distinguishing data -- dropped entirely per the "normalize, don't transliterate" rule,
-- confirmed by direct inspection of the dump (not just carried over from the prior analysis's
-- paraphrase). Only slots 1, 2, and the outlier slot 100000 carry real data.
IF
NOT EXISTS (SELECT 1 FROM world.BloodExchangeCatalog)
BEGIN
INSERT INTO world.BloodExchangeCatalog (BloodExchangeSlot, ItemId, Cost, Quantity)
VALUES (1, 12, 5, 0),
       (2, 1019, 9999, 1),
       (100000, 6, 0, 0);
END;
