CREATE TABLE world.ItemMallProducts
(
    ItemMallProductId INT     NOT NULL,
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
