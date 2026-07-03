-- Read-only shape for usp_AccountVault_SetItems' TVP parameter: one row per populated slot in an account's
-- shared vault. Whole-list replace (delete-then-insert against game.AccountVaultItems), matching the legacy
-- uSaveItem[MAX_SAVE_ITEM_SLOT_NUM][MAX_SAVE_ITEM_VALUE_NUM]/uSaveSocketGem arrays being saved as a whole
-- block, never slot-by-slot. No PK/index: passed by value per call, never stored.
CREATE TYPE game.tvp_AccountVaultItemSlot AS TABLE
    (
    SlotIndex SMALLINT NOT NULL,
    ItemId INT NULL,
    Quantity INT NOT NULL,
    Value INT NOT NULL,
    SerialNumber INT NOT NULL,
    SocketData NVARCHAR(50) NULL
    );
