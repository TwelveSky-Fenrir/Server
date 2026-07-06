-- Corrects op17 CL_CREATE_AVATAR_SEND2's starter-kit grant: the seed data in 082_starter_kit_equipment.sql
-- and usp_Character_CreateWithStarterKit both reproduced the WRONG compile-time branch of
-- Server/ts25login/S04_MyWork02.cpp's PreviousTribe switch.
--
-- Server/ts25login/S04_MyWork02.cpp:1 unconditionally `#define USE_CUSTOME_CREATE` at the very top of the
-- translation unit, before any other header -- there is no matching #undef anywhere under Server/ (verified:
-- a repo-wide search for "#undef USE_CUSTOME_CREATE" returns zero matches) and
-- Server/ts25login/ts25login.vcxproj has no ExcludedFromBuild condition on this file. Every other file in the
-- codebase gates the SAME-LOOKING feature behind the `M33`/`LNW33EU` build-variant chain
-- (Server/Header/Protocol/DEFINE.h:21-51), which is inactive for both shipped configurations
-- (Server/ts25latest_config.props:4-12) -- but this one file bypasses that chain entirely by forcing the
-- macro on for itself alone. The consequence: the `#else` arm of every `#ifdef USE_CUSTOME_CREATE` block in
-- this file (the "G12 Elite Normal Set" gear, the remapped weapons, and the boosted starting level/stats/
-- enchant below) is what actually ships, in every build configuration -- not the small-ID `#ifndef` arm
-- 082_starter_kit_equipment.sql originally seeded.
--
-- Elite equipment (Server/ts25login/S04_MyWork02.cpp:761-838, one race-keyed set of Armor/Gloves/Boots/Ring/
-- Amulet plus a raw-client-code -> elite-item weapon remap per race; all 24 elite item ids already exist in
-- world.Items via 080_items.sql). The C++ source's own inline comments mislabel which array index is Ring vs
-- Amulet (e.g. aEquip[0][0] is commented "// Ring" at line 770 but is actually assigned item 84671, "Wild
-- DemonSoul Necklace" per world.Items -- an amulet by name; aEquip[4][0] is commented "// Amulet" but gets
-- 84647, "Twin Head Demon Ring" -- a ring by name). This migration follows the FEQUIP_TYPE enum
-- (Server/Header/Protocol/STRUCT.h:1662-1676: EAMULET=0, ERING=4) and the real item names over the C++
-- file's own swapped comments.
--
-- Starting level/stats/enchant (Server/ts25login/S04_MyWork02.cpp:1100-1123): aLevel1 = MAX_LIMIT_LEVEL_NUM
-- (145, DEFINE.h:604), aLevel2 = 12, aRebirthNum = 0, aExp1 = MAX_NUMBER_SIZE (2,000,000,000,
-- DEFINE.h:365 -- not resolved by the originating behavior contract, independently located in this
-- migration), aExp2 = 0, aStatPoint = 3175, aSkillPoint = 10000, and SetISIUIMValue(45, 6, 0, 0) (Improve/
-- Enchant=45, Combine/CS=6) on Weapon/Armor/Gloves/Boots/Ring/Amulet (aEquip[X][2] for X in
-- {7,2,3,5,0,4}) -- despite the adjacent comment at line 1112 labelling this "Enchant[20%]", the literal
-- first argument passed is 45, not 20; this migration stores the literal 45/6 the code actually executes.
--
-- Starting mount (Server/ts25login/S04_MyWork02.cpp:1174-1179): aAnimal[0] = ANIMAL_NUM_TIGER1 (1301,
-- DEFINE.h:157), aAnimalExpActivity[0] = 0, aAnimalPower[0] = 5 ("5% Tiger" per the inline comment),
-- aAnimalIndex = 0, aAnimalTime = 99999999. No per-character mount table/array exists yet in this schema (no
-- other Fenrir feature reads or writes one), so this migration adds the same shape of plain scalar columns
-- already used for the single starting pet (PetGrowth/PetActivity) rather than introducing a multi-mount
-- inventory model nothing yet needs.
--
-- Deliberately NOT implemented here (see the originating behavior contract's own edge cases):
--   * Item quantities/duplication caps in world.StarterKitInventory: the given contract's claim that
--     aInventory[page][slot][1] is "quantity" (0/1/2/3) and aInventory[...][3] is a separate "duplication
--     cap" (999/999/999/10) is contradicted by direct verification in this task --
--     ServerDocs/12_ts25zone/06_MyWork03_Items_Equipement_Craft_Quetes.md:115-117 documents field [3] itself
--     as "quantité/durabilité", and Server/ts25zone/S04_MyWork03.cpp:613-628 (DecreaseQunatity) decrements
--     exactly that field ([3]) as the live, consumable stack count on item use. world.StarterKitInventory's
--     existing Quantity column already stores field [3]'s values (999/999/999/10) correctly -- left
--     unchanged.
--   * The second decoration ("Wing01"/EDECO2) and first decoration (EDECO1) equip slots
--     (S04_MyWork02.cpp:1143-1172): every PreviousTribe branch (including the switch's default) grants item
--     id 0 -- i.e. no item -- for EDECO2, and EDECO1's item id is never assigned at all in the cited range.
--     Both slots still get an enchant-style encoded field and a freshly generated serial in the legacy
--     source, but this schema's game.CharacterItems (like world.StarterKitSkills) represents "no item" as
--     row absence, and an ItemId of 0 would violate FK_CharacterItems_Item (world.Items has no id-0 row) --
--     so, consistent with that existing convention, no row is granted for either slot.
--   * Per-item Serial numbers on the pet/cape/decoration slots: legacy's ReturnItemSerial(3)
--     (Server/Header/function.h:5-26) returns a wall-clock-derived value, not a fixed legacy constant, and no
--     serial-issuing mechanism exists anywhere yet on the C# side to reproduce even the shape of that
--     behavior. Flagged for a future, separately-scoped feature rather than invented here.
ALTER TABLE world.StarterKitEquipment
    ADD RawWeaponCode TINYINT NULL;
GO

ALTER TABLE game.Characters
    ADD MountItemId      INT NOT NULL CONSTRAINT DF_Characters_MountItemId DEFAULT 0,      -- 0 = no mount; ANIMAL_NUM_TIGER1 (1301) once granted
        MountExpActivity INT NOT NULL CONSTRAINT DF_Characters_MountExpActivity DEFAULT 0, -- aAnimalExpActivity[0]
        MountPower       INT NOT NULL CONSTRAINT DF_Characters_MountPower DEFAULT 0,       -- aAnimalPower[0]
        MountSlotIndex   INT NOT NULL CONSTRAINT DF_Characters_MountSlotIndex DEFAULT -1,  -- aAnimalIndex; -1 = none active
        MountTime        INT NOT NULL CONSTRAINT DF_Characters_MountTime DEFAULT 0;        -- aAnimalTime
GO

-- RawWeaponCode is NULL for the 5 unconditional per-race grants (Amulet/Armor/Gloves/Ring/Boots) and holds
-- the raw client-selectable code for each of the 3 weapon alternatives per race -- CreateAvatarService
-- validates the client's chosen code against this column, then grants THIS row's ItemId (the elite weapon),
-- never the raw code itself.
CREATE OR ALTER PROCEDURE world.usp_StarterKit_GetByPreviousTribe @PreviousTribe TINYINT, @MapId SMALLINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT EquipSlot, ItemId, RawWeaponCode
    FROM world.StarterKitEquipment
    WHERE PreviousTribe = @PreviousTribe
    ORDER BY EquipSlot, ItemId;

    SELECT SlotIndex, ItemId, Quantity
    FROM world.StarterKitInventory
    ORDER BY SlotIndex;

    SELECT SlotIndex, SkillId, Grade
    FROM world.StarterKitSkills
    WHERE PreviousTribe = @PreviousTribe
    ORDER BY SlotIndex;

    SELECT Page, KeyIndex, Sort, Value1, Value2
    FROM world.StarterKitHotkeys
    WHERE PreviousTribe = @PreviousTribe
    ORDER BY Page, KeyIndex;

    SELECT DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ
    FROM world.Zones
    WHERE ZoneNumber = @MapId;
END;
GO

-- Adds the starting level/stat/skill-point/mount grant to the INSERT (all literal legacy constants, like the
-- existing StatVit/StatStr/StatInt/StatDex=1 and PetGrowth/PetActivity=640000000/100 immediately above them)
-- -- see this migration file's own header comment for citations. @Equipment's Enchant/Combine columns now
-- carry CreateAvatarService's SetISIUIMValue(45, 6, 0, 0) encoding for the 6 elite-gear categories; nothing
-- else about this proc's shape changes.
CREATE OR ALTER PROCEDURE game.usp_Character_CreateWithStarterKit
    @AccountId               INT,
    @Slot                    TINYINT,
    @Name                    NVARCHAR(13),
    @Tribe                   TINYINT,
    @Gender                  TINYINT,
    @HeadType                TINYINT,
    @FaceType                TINYINT,
    @MapId                   SMALLINT,
    @PosX                    REAL,
    @PosY                    REAL,
    @PosZ                    REAL,
    @Life                    INT,
    @MaxLife                 INT,
    @Mana                    INT,
    @MaxMana                 INT,
    @WelcomeBuffUntilDate    INT,
    @PremiumUntilUnixSeconds BIGINT,
    @Equipment               game.tvp_CharacterItemSlot READONLY,
    @Inventory               game.tvp_CharacterItemSlot READONLY,
    @Skills                  game.tvp_CharacterSkillSlot READONLY,
    @Hotkeys                 game.tvp_CharacterHotkeySlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, 'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, 'Character name already taken.', 1;

    DECLARE @CharacterId TABLE (CharacterId INT);

    -- EU33 defaults: every stat starts at 1 (not the column default of 0); every new character is granted the
    -- same starter pet growth/activity (200% growth, full activity) and the same starting level/rebirth/
    -- experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179, forced on by this
    -- file's own USE_CUSTOME_CREATE -- see this migration's header comment) regardless of tribe. The pet/cape
    -- item rows themselves travel in via @Equipment alongside the tribe's elite armor/gloves/boots/ring/
    -- amulet/weapon.
    INSERT INTO game.Characters
        (AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType,
         MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
         StatVit, StatStr, StatInt, StatDex,
         PetGrowth, PetActivity,
         Level, Level2, RebirthCount, Experience, Exp2,
         StatPoints, SkillPoints,
         MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
         DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
        OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES
        (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType,
         @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana,
         1, 1, 1, 1,
         640000000, 100,
         145, 12, 0, 2000000000, 0,
         3175, 10000,
         1301, 0, 5, 0, 99999999,
         @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

    DECLARE @NewCharacterId INT = (SELECT CharacterId FROM @CharacterId);

    INSERT INTO game.CharacterItems
        (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
         SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId, 2, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @Equipment;

    INSERT INTO game.CharacterItems
        (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
         SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId, 0, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @Inventory;

    INSERT INTO game.CharacterSkills (CharacterId, SlotIndex, SkillId, Grade)
    SELECT @NewCharacterId, SlotIndex, SkillId, Grade
    FROM @Skills;

    INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2)
    SELECT @NewCharacterId, Page, KeyIndex, Sort, Value1, Value2
    FROM @Hotkeys;

    SELECT @NewCharacterId AS CharacterId;
END;
GO
