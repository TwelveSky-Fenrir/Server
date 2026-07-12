-- TVP for usp_OfflineShop_OpenAndReplaceContainers: whole-list replace of a shop's slots at open time,
-- mirroring the legacy client's whole-array PROXY_SHOP_USER_INFO save. A standalone usp_OfflineShop_SetItems
-- (whole-list replace outside the open flow) used to exist but had zero callers -- OpenAndReplaceContainers
-- is the only path that ever writes this shape -- removed as dead code in the 2026-07-12 Database/ cleanup
-- pass (dead-file-sweep-tables-procs finding).
CREATE TYPE game.tvp_OfflineShopItemSlot AS TABLE
(
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL,
    Value        INT          NOT NULL,
    SerialNumber INT          NOT NULL,
    Price        INT          NOT NULL,
    SocketData   NVARCHAR(50) NULL
);
