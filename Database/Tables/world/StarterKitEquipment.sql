CREATE TABLE world.StarterKitEquipment
(
    PreviousTribe TINYINT NOT NULL,
    EquipSlot     TINYINT NOT NULL,
    ItemId        INT     NOT NULL,
    RawWeaponCode TINYINT NULL, 
    CONSTRAINT PK_StarterKitEquipment PRIMARY KEY CLUSTERED (PreviousTribe, EquipSlot, ItemId),
    CONSTRAINT CK_StarterKitEquipment_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    CONSTRAINT FK_StarterKitEquipment_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
