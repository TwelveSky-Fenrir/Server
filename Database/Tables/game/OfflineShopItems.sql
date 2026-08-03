CREATE TABLE game.OfflineShopItems
(
    CharacterId  INT          NOT NULL,
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL
        CONSTRAINT DF_OfflineShopItems_Quantity DEFAULT 0,
    Value        INT          NOT NULL
        CONSTRAINT DF_OfflineShopItems_Value DEFAULT 0,
    SerialNumber INT          NOT NULL
        CONSTRAINT DF_OfflineShopItems_SerialNumber DEFAULT 0,
    Price        INT          NOT NULL
        CONSTRAINT DF_OfflineShopItems_Price DEFAULT 0,
    SocketData   NVARCHAR(50) NULL,
    CONSTRAINT PK_OfflineShopItems PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_OfflineShopItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 24),
    CONSTRAINT CK_OfflineShopItems_Quantity CHECK (Quantity BETWEEN 1 AND 999), -- MAX_ITEM_DUPLICATION_NUM (Server/Header/Protocol/DEFINE.h:609)
    CONSTRAINT FK_OfflineShopItems_Shop FOREIGN KEY (CharacterId) REFERENCES game.OfflineShops (CharacterId) ON DELETE CASCADE,
    CONSTRAINT FK_OfflineShopItems_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
