-- Normalizes legacy masterinfo's packed uSaveItem VARCHAR(728)/uSaveSocketGem VARCHAR(756) strings
-- (MAX_SAVE_ITEM_SLOT_NUM=28 slots) into one row per populated vault slot.
-- Quantity/Value/SerialNumber are real columns beyond the task brief's suggested shape: the packed format
-- matches PROXY_SHOP_ITEM's fields minus Price, so this data is recoverable and kept rather than dropped.
-- ItemId 0 (empty slot in the packed source) translates to NULL, not a 0 FK.
CREATE TABLE game.AccountVaultItems
(
    AccountId    INT          NOT NULL,
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_Quantity DEFAULT 0,
    Value        INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_Value DEFAULT 0,
    SerialNumber INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_SerialNumber DEFAULT 0,
    SocketData   NVARCHAR(50) NULL,
    CONSTRAINT PK_AccountVaultItems PRIMARY KEY CLUSTERED (AccountId, SlotIndex),
    CONSTRAINT CK_AccountVaultItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT FK_AccountVaultItems_Vault FOREIGN KEY (AccountId) REFERENCES game.AccountVault (AccountId) ON DELETE CASCADE,
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention -- the FK above stays bare (same-schema, game -> game).
    CONSTRAINT FK_AccountVaultItems_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
    -- No secondary index on ItemId: verified repo-wide that every reader drives its seek from AccountId (the
    -- leading clustered-PK column); nothing ever seeks this table BY ItemId. usp_AccountVault_SetItems does a
    -- full container DELETE-then-INSERT on essentially every vault transfer, so an unused index here is pure
    -- per-write maintenance cost with zero read benefit.
);
