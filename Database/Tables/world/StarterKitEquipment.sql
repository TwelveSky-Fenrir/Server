-- Legacy: Server/ts25login/S04_MyWork02.cpp CREATE_AVATAR_SEND2, USE_CUSTOME_CREATE branch (force-defined
-- unconditionally at S04_MyWork02.cpp:1, no #undef anywhere under Server/ -- this is the branch that ships
-- in every build configuration). Keyed by PreviousTribe (0=Noble Dragon,1=Royal Serpent,2=Grand Tiger), the
-- client-chosen starting-kit template -- distinct from the playable Tribe (0-3) used for spawn/faction.
-- EquipSlot follows FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h:1662-1676): 0=Amulet,2=Armor,3=Gloves,
-- 4=Ring,5=Boots,7=Weapon. Amulet/Armor/Gloves/Ring/Boots each own exactly one row per PreviousTribe;
-- Weapon owns the 3 client-selectable alternatives (CreateAvatarRequest.Weapon, matched via RawWeaponCode,
-- must match one of them) -- 8 rows per PreviousTribe, 24 total.
CREATE TABLE world.StarterKitEquipment
(
    PreviousTribe TINYINT NOT NULL,
    EquipSlot     TINYINT NOT NULL,
    ItemId        INT     NOT NULL,
    RawWeaponCode TINYINT NULL, -- NULL for the 5 unconditional per-race grants; holds the raw client-selectable weapon code (5/6/7, 11/12/13, or 17/18/19) for each of the 3 Weapon rows per race
    CONSTRAINT PK_StarterKitEquipment PRIMARY KEY CLUSTERED (PreviousTribe, EquipSlot, ItemId),
    CONSTRAINT CK_StarterKitEquipment_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    CONSTRAINT FK_StarterKitEquipment_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
