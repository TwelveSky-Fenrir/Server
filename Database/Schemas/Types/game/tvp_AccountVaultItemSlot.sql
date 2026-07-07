-- TVP for usp_AccountVault_SetItems: whole-list replace, mirroring the legacy uSaveItem/uSaveSocketGem
-- arrays being saved as a whole block.
CREATE TYPE game.tvp_AccountVaultItemSlot AS TABLE
(
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL,
    Value        INT          NOT NULL,
    SerialNumber INT          NOT NULL,
    SocketData   NVARCHAR(50) NULL
);
