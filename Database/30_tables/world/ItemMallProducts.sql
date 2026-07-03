-- Legacy MySQL `itemmallinfo` (nxtserver.sql): the cash-shop catalog GameServer loads once at boot
-- into an in-memory cache -- world.* is read-mostly reference data, never queried per-tick.
-- ItemMallProductId is the legacy `Number`: the client's cash-shop purchase request names a product
-- by this exact number, so it is stored as-given, never IDENTITY -- including the special slot
-- 100000, one extra product outside the normal 1..560 sequence (the dump uses the same "one outlier
-- row past the sequential block" pattern for bloodinfo's own 100000 row -- see BloodExchangeCatalog).
-- ItemId is NULL, never 0: the dump uses ItemID=0 for a disabled/empty slot, and 0 is not a valid
-- world.Items row, so 0 is translated to NULL at seed time (shared cross-domain FK convention). A
-- slot can still be a real, configured product with IsActive=0 (temporarily withdrawn from sale,
-- e.g. Number 282/284/etc. in the dump) -- those rows are kept; only the fully-empty ItemID=0/
-- Cost=0/Quantity=0/Active=0 filler rows (~400 of the dump's 561) are dropped entirely rather than
-- transliterated, since they carry no information (see 70_seed/world/010_item_mall_products.sql).
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
