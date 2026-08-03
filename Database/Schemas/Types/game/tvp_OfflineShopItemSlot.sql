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
