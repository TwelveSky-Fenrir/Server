-- Normalized from nShopInfo[MAX_NPC_SHOP_PAGE_NUM=3][MAX_NPC_SHOP_SLOT_NUM=28] (Header/Protocol/
-- STRUCT.h:213-234): only nonzero slots are kept -- genuinely sparse, unlike NpcMenuOptions. Only
-- 34 of the 131 real NPCs sell anything at all, and across those 34 only 467 of the 3*28=84
-- slots-per-NPC ceiling (11004 across all real NPCs) are populated.
-- ItemId is NULL, never 0, for the same reason as the other world.* tables in this batch -- kept
-- NULLable/FK'd defensively per the shared cross-domain FK convention even though this generator never
-- actually emits a zero/NULL row (a shop slot with no item simply isn't a row at all here).
CREATE TABLE world.NpcShopItems
(
    NpcId     INT     NOT NULL,
    ShopPage  TINYINT NOT NULL,
    SlotIndex TINYINT NOT NULL,
    ItemId    INT NULL,
    CONSTRAINT PK_NpcShopItems PRIMARY KEY CLUSTERED (NpcId, ShopPage, SlotIndex),
    CONSTRAINT CK_NpcShopItems_ShopPage CHECK (ShopPage BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcShopItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT FK_NpcShopItems_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_NpcShopItems_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
