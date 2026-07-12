-- Server/ts25login/S04_MyWork02.cpp CREATE_AVATAR_SEND2, USE_CUSTOME_CREATE branch (force-defined
-- unconditionally at S04_MyWork02.cpp:1, no #undef anywhere under Server/ -- this is the branch that ships
-- in every build configuration): the "G12 Elite Normal Set" (S04_MyWork02.cpp:766-836).
--
-- EquipSlot follows FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h:1662-1676): 0=Amulet, 2=Armor, 3=Gloves,
-- 4=Ring, 5=Boots, 7=Weapon. The C++ source's own inline comments swap Ring/Amulet
-- (S04_MyWork02.cpp:770-771,1119-1120); this seed follows the enum + world.Items' real item names instead.
--
-- All 24 elite item ids below already exist in world.Items (080_items.sql) under their real names.
IF
    NOT EXISTS (SELECT 1
                FROM world.StarterKitEquipment)
    BEGIN
        INSERT INTO world.StarterKitEquipment (PreviousTribe, EquipSlot, ItemId, RawWeaponCode)
        VALUES
            -- Noble Dragon (PreviousTribe 0): G12 Elite Normal Set (S04_MyWork02.cpp:761-781)
            (0, 0, 84671, NULL), -- Amulet - Wild DemonSoul Necklace
            (0, 2, 84575, NULL), -- Armor - Kahn Guardian Armor
            (0, 3, 84623, NULL), -- Gloves - Glorious Fist Wristband
            (0, 4, 84647, NULL), -- Ring - Twin Head Demon Ring
            (0, 5, 84599, NULL), -- Boots - Island Strider Boots
            (0, 7, 84503, 5),    -- Weapon: raw code 5 (Sword) -> Dragon's Fang Sword
            (0, 7, 84527, 6),    -- raw code 6 (Blade) -> Blade of the Moon
            (0, 7, 84551, 7),    -- raw code 7 (Marble) -> Great Dragon Eye Marble

            -- Royal Serpent (PreviousTribe 1) (S04_MyWork02.cpp:783-809)
            (1, 0, 85671, NULL), -- Amulet - Blue Sphere
            (1, 2, 85575, NULL), -- Armor - Everlasting Gold Armor
            (1, 3, 85623, NULL), -- Gloves - Eternal Gold Vambrace
            (1, 4, 85647, NULL), -- Ring - Lunar Cycle Ring
            (1, 5, 85599, NULL), -- Boots - Everlasting Gold Boots
            (1, 7, 85503, 11),   -- Katana - Thousand Lights
            (1, 7, 85527, 12),   -- Double Blades - Black Feast
            (1, 7, 85551, 13),   -- Mandolin - Silversong

            -- Grand Tiger (PreviousTribe 2) (S04_MyWork02.cpp:811-838)
            (2, 0, 86671, NULL), -- Amulet - Haung Long's Heart
            (2, 2, 86575, NULL), -- Armor - Xuan Wu's Tenacity
            (2, 3, 86623, NULL), -- Gloves - 28 Houses Gauntlet
            (2, 4, 86647, NULL), -- Ring - Haung Long's Claw
            (2, 5, 86599, NULL), -- Boots - Four Cardinal Striders
            (2, 7, 86503, 17),   -- Light Blade - Baihu's Fang
            (2, 7, 86527, 18),   -- Spear - Qing Long's Grace
            (2, 7, 86551, 19);   -- Scepter - Zhu Que's Spirit
    END;
