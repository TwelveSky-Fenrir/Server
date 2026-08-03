CREATE TYPE game.tvp_AccountVaultItemSlotV2 AS TABLE
(
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL,
    Value        INT          NOT NULL,
    SerialNumber INT          NOT NULL,
    SocketData   NVARCHAR(50) NULL,
    SocketGem1   INT          NOT NULL,
    SocketGem2   INT          NOT NULL,
    SocketGem3   INT          NOT NULL,
    ExpireDate   INT          NOT NULL
);
