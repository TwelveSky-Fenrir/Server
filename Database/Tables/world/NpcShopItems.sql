-- Normalized from nShopInfo[3][28]; sparse (unlike NpcMenuOptions) -- only 34 of 131 real NPCs sell anything, and few of their 84 possible slots are populated.
CREATE TABLE world.NpcShopItems
(
    NpcId     INT     NOT NULL,
    ShopPage  TINYINT NOT NULL,
    SlotIndex TINYINT NOT NULL,
    ItemId    INT     NULL,
    CONSTRAINT PK_NpcShopItems PRIMARY KEY CLUSTERED (NpcId, ShopPage, SlotIndex),
    CONSTRAINT CK_NpcShopItems_ShopPage CHECK (ShopPage BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcShopItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT FK_NpcShopItems_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_NpcShopItems_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    INDEX IX_NpcShopItems_ItemId NONCLUSTERED (ItemId)
);
