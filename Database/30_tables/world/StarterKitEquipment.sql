-- Legacy: Server/ts25login/S04_MyWork02.cpp CREATE_AVATAR_SEND2, non-USE_CUSTOME_CREATE branch (the one
-- LNW33/EU33 builds compile). Keyed by PreviousTribe (0=Noble Dragon,1=Royal Serpent,2=Grand Tiger), the
-- client-chosen starting-kit template -- distinct from the playable Tribe (0-3) used for spawn/faction.
-- EquipSlot follows FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h): 2=Armor,3=Gloves,5=Boots,7=Weapon.
-- Armor/Gloves/Boots each own exactly one row per PreviousTribe; Weapon owns the 3 client-selectable
-- alternatives (CreateAvatarRequest.Weapon must match one of them).
CREATE TABLE world.StarterKitEquipment
(
    PreviousTribe TINYINT NOT NULL,
    EquipSlot     TINYINT NOT NULL,
    ItemId        INT     NOT NULL,
    CONSTRAINT PK_StarterKitEquipment PRIMARY KEY CLUSTERED (PreviousTribe, EquipSlot, ItemId),
    CONSTRAINT CK_StarterKitEquipment_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    CONSTRAINT FK_StarterKitEquipment_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
