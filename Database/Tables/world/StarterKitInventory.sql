-- Legacy: same function's unconditional aInventory[0][0..3] block -- identical for every tribe/template,
-- so unlike StarterKitEquipment/Skills/Hotkeys this table carries no tribe key at all.
CREATE TABLE world.StarterKitInventory
(
    SlotIndex TINYINT NOT NULL,
    ItemId    INT     NOT NULL,
    Quantity  INT     NOT NULL,
    CONSTRAINT PK_StarterKitInventory PRIMARY KEY CLUSTERED (SlotIndex),
    CONSTRAINT FK_StarterKitInventory_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    -- Quantity is a stack count, bounded by the same MAX_ITEM_DUPLICATION_NUM=999 stack ceiling already
    -- cited on world.StarterKitHotkeys' seed data (Migrations/Seed/world/011_starter_kit_hotkeys.sql); a
    -- starter-kit grant is never a literal zero-quantity row (see 009_starter_kit_inventory.sql: 999/999/999/10).
    CONSTRAINT CK_StarterKitInventory_Quantity CHECK (Quantity BETWEEN 1 AND 999)
);
