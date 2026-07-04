-- Legacy MySQL `bloodinfo` (a "blood exchange" trade-in catalog); BloodExchangeSlot is the legacy `Number`, stored as-given (including outlier slot 100000, same pattern as ItemMallProducts). Empty filler slots are not represented as rows.
CREATE TABLE world.BloodExchangeCatalog
(
    BloodExchangeSlot INT NOT NULL,
    ItemId            INT NULL,
    Cost              INT NOT NULL CONSTRAINT DF_BloodExchangeCatalog_Cost DEFAULT 0,
    Quantity          INT NOT NULL CONSTRAINT DF_BloodExchangeCatalog_Quantity DEFAULT 0,
    CONSTRAINT PK_BloodExchangeCatalog PRIMARY KEY CLUSTERED (BloodExchangeSlot),
    CONSTRAINT FK_BloodExchangeCatalog_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
