-- Legacy: same function's unconditional aInventory[0][0..3] block -- identical for every tribe/template,
-- so unlike StarterKitEquipment/Skills/Hotkeys this table carries no tribe key at all.
CREATE TABLE world.StarterKitInventory
(
    SlotIndex TINYINT NOT NULL,
    ItemId    INT     NOT NULL,
    Quantity  INT     NOT NULL,
    CONSTRAINT PK_StarterKitInventory PRIMARY KEY CLUSTERED (SlotIndex),
    CONSTRAINT FK_StarterKitInventory_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
