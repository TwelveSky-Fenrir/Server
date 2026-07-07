-- Server/ts25login/S04_MyWork02.cpp CREATE_AVATAR_SEND2, non-USE_CUSTOME_CREATE branch (EU33/LNW33 builds).
-- EquipSlot per FEQUIP_TYPE: 2=Armor, 3=Gloves, 5=Boots, 7=Weapon (3 client-selectable alternatives).
IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitEquipment)
    BEGIN
        INSERT INTO world.StarterKitEquipment (PreviousTribe, EquipSlot, ItemId)
        VALUES
            -- Noble Dragon (PreviousTribe 0): Earth Armor/Gloves/Boots + Mulan Sword/Blade/Ocean Marble
            (0, 2, 8),
            (0, 3, 9),
            (0, 5, 10),
            (0, 7, 5),
            (0, 7, 6),
            (0, 7, 7),
            -- Royal Serpent (PreviousTribe 1): Vine Armor/Gloves/Boots + Wuji Katana/Double Blades/Mandolin
            (1, 2, 14),
            (1, 3, 15),
            (1, 5, 16),
            (1, 7, 11),
            (1, 7, 12),
            (1, 7, 13),
            -- Grand Tiger (PreviousTribe 2): Kuangmo Armor/Gloves/Boots + Heimo Light Blade/Long Spear/Scepter
            (2, 2, 20),
            (2, 3, 21),
            (2, 5, 22),
            (2, 7, 17),
            (2, 7, 18),
            (2, 7, 19);
    END;
