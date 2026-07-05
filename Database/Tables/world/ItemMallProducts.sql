-- Legacy MySQL `itemmallinfo`; ItemMallProductId is the legacy `Number`, stored as-given (including outlier slot 100000, same pattern as BloodExchangeCatalog).
-- Fully-empty filler rows (ItemId=0/Cost=0/Quantity=0/IsActive=0) are dropped entirely, not seeded as empty rows.
CREATE TABLE world.ItemMallProducts
(
    ItemMallProductId INT     NOT NULL,
    ProductType       TINYINT NOT NULL, -- legacy `Type`: which cash-shop tab/page lists this product (values observed: 1-5)
    ItemId            INT NULL,
    Quantity          INT     NOT NULL CONSTRAINT DF_ItemMallProducts_Quantity DEFAULT 0,
    Cost              INT     NOT NULL CONSTRAINT DF_ItemMallProducts_Cost DEFAULT 0,
    IsActive          BIT     NOT NULL CONSTRAINT DF_ItemMallProducts_IsActive DEFAULT 0,
    CONSTRAINT PK_ItemMallProducts PRIMARY KEY CLUSTERED (ItemMallProductId),
    CONSTRAINT FK_ItemMallProducts_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
