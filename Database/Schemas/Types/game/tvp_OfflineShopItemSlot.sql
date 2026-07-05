-- TVP for usp_OfflineShop_SetItems: whole-list replace of a shop's slots, mirroring the legacy client's
-- whole-array PROXY_SHOP_USER_INFO save.
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
