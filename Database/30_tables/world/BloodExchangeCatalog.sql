-- Legacy MySQL `bloodinfo` (nxtserver.sql): a "blood exchange" trade-in catalog (spend a currency
-- to obtain an item), loaded once at boot the same way as the rest of world.*. BloodExchangeSlot is
-- the legacy `Number` -- stored as-given, never IDENTITY, including the special slot 100000 (bloodinfo
-- breaks its own 1..50 sequence for one extra row, the same outlier-row pattern itemmallinfo uses).
-- Only 3 of the dump's 51 rows carry real data (slots 1, 2, 100000); slots 3-50 are ItemID=0/Cost=0/
-- Quantity=0 filler with no meaning, so they are not represented as rows at all -- normalizing, not
-- transliterating, the legacy administrative padding (see 70_seed/world/011_blood_exchange_catalog.sql).
-- ItemId is NULL, never 0, for the same reason as world.ItemMallProducts (shared FK convention).
CREATE TABLE world.BloodExchangeCatalog
(
    BloodExchangeSlot INT NOT NULL,
    ItemId            INT NULL,
    Cost              INT NOT NULL CONSTRAINT DF_BloodExchangeCatalog_Cost DEFAULT 0,
    Quantity          INT NOT NULL CONSTRAINT DF_BloodExchangeCatalog_Quantity DEFAULT 0,
    CONSTRAINT PK_BloodExchangeCatalog PRIMARY KEY CLUSTERED (BloodExchangeSlot),
    CONSTRAINT FK_BloodExchangeCatalog_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
