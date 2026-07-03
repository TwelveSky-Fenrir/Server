-- Read-only shape for usp_OfflineShop_SetItems' TVP parameter: one row per populated sale slot the caller
-- wants to persist for one character's shop. Whole-list replace (delete-then-insert against
-- game.OfflineShopItems), not a per-slot upsert, because the client submits its entire shop layout in one
-- shot when a shop is opened/edited (Protocol/STRUCT.h PROXY_SHOP_USER_INFO is populated/saved as a whole
-- tItem[MAX_PSHOP_PAGE_NUM][MAX_PSHOP_SLOT_NUM] array, never slot-by-slot). No PK/index: passed by value
-- per call, never stored.
CREATE TYPE game.tvp_OfflineShopItemSlot AS TABLE
    (
    SlotIndex SMALLINT NOT NULL,
    ItemId INT NULL,
    Quantity INT NOT NULL,
    Value INT NOT NULL,
    SerialNumber INT NOT NULL,
    Price INT NOT NULL,
    SocketData NVARCHAR(50) NULL
    );
