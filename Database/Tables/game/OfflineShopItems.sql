-- Normalizes legacy `proxyinfo`'s packed aItem/aSocket strings (MAX_PSHOP_PAGE_NUM=5 x MAX_PSHOP_SLOT_NUM=5
-- = 25 slots) into one row per populated sale slot; SlotIndex 0-24 is PAGE*5+SLOT.
-- Quantity/Value/SerialNumber/Price are real columns beyond the task brief's suggested shape -- the legacy
-- PROXY_SHOP_ITEM struct carries them (notably Price, without which a listing can't be sold).
-- ItemId 0 (empty slot in the packed source) translates to NULL, not a 0 FK.
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
    CONSTRAINT FK_OfflineShopItems_Shop FOREIGN KEY (CharacterId) REFERENCES game.OfflineShops (CharacterId) ON DELETE CASCADE,
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention -- the FK above stays bare (same-schema, game -> game).
    CONSTRAINT FK_OfflineShopItems_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
    -- No secondary index on ItemId: verified repo-wide that every reader drives its seek from CharacterId (the
    -- leading clustered-PK column) or a CAS-style guarded DELETE where ItemId is an extra WHERE predicate
    -- evaluated after the PK already narrowed to <=1 row (usp_OfflineShop_ExecutePurchase,
    -- usp_OfflineShop_RetrieveItemAndReplaceContainer); vw_OfflineShopListing.sql's own join drives from
    -- world.Items (already PK'd), not from this table, so it doesn't exercise an ItemId index either. Meanwhile
    -- this table gets a full container DELETE-then-INSERT on essentially every listing change, so an unused
    -- index here is pure per-write maintenance cost with zero read benefit.
);
