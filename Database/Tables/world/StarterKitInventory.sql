CREATE TABLE world.StarterKitInventory
(
    SlotIndex TINYINT NOT NULL,
    ItemId    INT     NOT NULL,
    Quantity  INT     NOT NULL,
    CONSTRAINT PK_StarterKitInventory PRIMARY KEY CLUSTERED (SlotIndex),
    CONSTRAINT FK_StarterKitInventory_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_StarterKitInventory_Quantity CHECK (Quantity BETWEEN 1 AND 999)
);
