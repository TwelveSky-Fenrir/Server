CREATE TABLE world.BloodExchangeCatalog
(
    BloodExchangeSlot INT NOT NULL,
    ItemId            INT NULL,
    Cost              INT NOT NULL
        CONSTRAINT DF_BloodExchangeCatalog_Cost DEFAULT 0,
    Quantity          INT NOT NULL
        CONSTRAINT DF_BloodExchangeCatalog_Quantity DEFAULT 0,
    CONSTRAINT PK_BloodExchangeCatalog PRIMARY KEY CLUSTERED (BloodExchangeSlot),
    CONSTRAINT FK_BloodExchangeCatalog_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
