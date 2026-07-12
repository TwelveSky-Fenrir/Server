-- Legacy MySQL `itemmallinfo`; ItemMallProductId is the legacy `Number`, stored as-given (including outlier slot 100000, same pattern as BloodExchangeCatalog).
-- Fully-empty filler rows (ItemId=0/Cost=0/Quantity=0/IsActive=0) are dropped entirely, not seeded as empty rows.
CREATE TABLE world.ItemMallProducts
(
    ItemMallProductId INT     NOT NULL,
    -- legacy `Type`: 1-4 = MAX_CASH_TYPE cash-shop tabs (Server/Header/Protocol/DEFINE.h:551,
    -- Server/ts25extra/S08_MyDB.cpp:152-154 loads WHERE Type=1..4); 5 is reserved exclusively for the
    -- version/CRC "ghost row" sentinels (Number=100000/100001, S08_MyDB.cpp:241,257), documented in
    -- ServerDocs/14_ts25extra/01_CashShop_ProxyShop_Gifts.md:29-31,76-99. CashCatalogBuilder.cs also
    -- resolves a third sentinel, ItemMallProductId=100002, under the same ProductType=5 convention.
    ProductType       TINYINT NOT NULL,
    ItemId            INT     NULL,
    Quantity          INT     NOT NULL
        CONSTRAINT DF_ItemMallProducts_Quantity DEFAULT 0,
    Cost              INT     NOT NULL
        CONSTRAINT DF_ItemMallProducts_Cost DEFAULT 0,
    IsActive          BIT     NOT NULL
        CONSTRAINT DF_ItemMallProducts_IsActive DEFAULT 0,
    CONSTRAINT PK_ItemMallProducts PRIMARY KEY CLUSTERED (ItemMallProductId),
    CONSTRAINT FK_ItemMallProducts_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_ItemMallProducts_ProductType CHECK (ProductType BETWEEN 1 AND 5)
);
