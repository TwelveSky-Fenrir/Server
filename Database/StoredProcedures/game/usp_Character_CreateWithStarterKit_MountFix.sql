-- Recree usp_Character_CreateWithStarterKit pour corriger la monture de depart.
--
-- La version initiale octroyait 1301 (ANIMAL_NUM_TIGER1) avec une puissance de 5. Le legacy octroie
-- 1314 (ANIMAL_NUM_BEAR2) avec une puissance de 10 -- Server/ts25login/S04_MyWork02.cpp:1163-1166, dont le
-- commentaire dit explicitement "Starting Mount: 10% Bear", et Server/Header/Protocol/DEFINE.h:165.
-- Le commentaire d'en-tete de la version initiale citait ANIMAL_NUM_TIGER1 pour une ligne qui n'a jamais
-- correspondu au legacy : c'etait une derive, pas une decision produit. La decision produit assumee
-- (niveau 1, 50 points de stats, 2 slots) est bornee a ces trois points et conserve le bloc
-- pet / cape / aile / monture tel quel.
--
-- Script ADDITIF : le script d'origine reste inchange, DbMigrator le journalise par SHA-256 et refuserait
-- de le reappliquer s'il etait edite. CREATE OR ALTER, comme usp_OfflineShop_ExecutePurchase.sql.
--
-- Changement COUPLE avec les constantes MountGrantItemId / MountGrantPower de
-- src/Fenrir.Application.Login/Services/CreateAvatar/CreateAvatarService.cs : le paquet
-- LC_CREATE_AVATAR_RECV annonce au client la monture que la base vient d'ecrire. Les deux doivent bouger
-- ensemble, sinon le client affiche une monture absente de la base au login suivant.

-- op17 CL_CREATE_AVATAR_SEND2's full creation path: usp_Character_Create's slot/name guards plus the starter
-- kit in the same transaction. Left as a separate proc from usp_Character_Create rather than extending it in
-- place: that proc is still the minimal-dependency character factory every other feature's test suite uses to
-- satisfy game.Characters' FK, and giving every one of those call sites this many new required parameters
-- (plus 4 TVPs) would be unrelated churn.
--
-- CONFIRMED PRODUCT DECISION (character-creation-level1-redesign, NOT a legacy-parity fix): a freshly created
-- character starts at genuine Level 1 with only a basic weapon + torso/chest armor piece equipped, NOT the
-- EU33/USE_CUSTOME_CREATE instant-elite grant (Level 145, 6-slot Enchant45/Combine6 gear) that the original
-- EU33-parity version of this procedure used -- see Tables/world/StarterKitEquipment.sql's own header for the
-- full USE_CUSTOME_CREATE citation this deliberately departs from. The starter pet/cape/wing/mount grant and
-- the welcome-buff counters/premium day below are NOT part of that rejected block (see their own bullets).
--   * StatVit/StatStr/StatInt/StatDex = 1 (the Level-1 floor); Level/Level2/RebirthCount/Experience/Exp2 =
--     1/0/0/0/0 (genuine Level 1, zero rebirths, no post-cap ladder progress).
--   * StatPoints/SkillPoints = 50/0: Fenrir product defaults, NOT legacy-cited (no compiled non-
--     USE_CUSTOME_CREATE branch exists anywhere in the reviewed Server/ts25login source to draw a level-1
--     starting pool from -- see CreateAvatarService.StartingStatPoint's own remarks). SkillPoints starts at 0
--     since the starter kit already grants every starting skill directly via game.CharacterSkills
--     (BuildSkillRows/world.StarterKitSkills).
--   * PetGrowth/PetActivity and the Mount* columns ARE granted here, independently of the level/rebirth/
--     stat-pool rejection above: Server/ts25login/S04_MyWork02.cpp:1131-1179 (pet/cape/wing/mount) carries no
--     USE_CUSTOME_CREATE gate of its own -- it sits right after that macro's own closing #endif at :1123.
--     PetActivity=100 (MAX_PAT_ACTIVITY_SIZE, DEFINE.h:612); PetGrowth=640000000 (`tAvatarInfo.aEquip[EPET][2]
--     = 640000000; // 200%`, Server/ts25login/S04_MyWork02.cpp:1134, matching the 640_000_000 top-tier cap
--     PetGrowthCaps.Values already lists). MountItemId=1314 (ANIMAL_NUM_BEAR2, DEFINE.h:165);
--     MountSlotIndex=0 overwrites the -1 "none active" sentinel so the character starts already mounted;
--     MountTime=99999999 (no practically reachable expiry, S04_MyWork02.cpp:1174-1179).
--     game.CharacterItems also gains Slot 8 (EPET)/ItemId 2300 and Slot 1 (ECAPE)/ItemId 1407 via the same
--     @Equipment TVP the weapon/armor rows already use -- see CreateAvatarService.BuildUnconditionalStarterGrantRows.
--     The source additionally stamps a raw "stat" value onto the Cape slot (40, "120%" tier) and onto an
--     itemless wing/"Deco2" slot (item id always 0 in every branch) -- neither has a home in game.CharacterItems
--     here: Enchant/Combine on item 1407 specifically don't drive any bonus formula (StatCalculator's cape-
--     defense bonus needs CheckSetItem=2 or Sort=29, neither true for 1407), so both are left unset rather
--     than guessed, and the itemless wing/"Deco1" slots get no row at all (row absence = empty slot here;
--     an ItemId=0 row would also violate FK_CharacterItems_World_Item).
--   * ProtectForDeath/AutoTime2/DoubleExpTime1/DoubleExpTime2 = 5/1440/300/300: unlike the instant-elite
--     gear/level-cap/mount block above, these four welcome-grant counters are gated only by the LNW33 macro
--     (confirmed live in both ReleaseM33 and the shipped ReleaseEU33, not a single-variant override --
--     Server/ts25login/S04_MyWork02.cpp:885-893), never by USE_CUSTOME_CREATE -- so they are applied here
--     rather than rejected alongside it.
--   * AutoBuffTime/InventoryDate/StoreDate stay @WelcomeBuffUntilDate (today + 7 days): the welcome-buff/
--     second-inventory-page/second-store-page rental grant is independent of the old EU33 instant-elite block
--     and survives the redesign.
--   * PremiumExpireUtc = @PremiumUntilUnixSeconds (CreateAvatarService.cs computes creation time + 1 day):
--     this one-day extension carries no macro guard at all in legacy (Server/ts25login/S04_MyWork02.cpp:
--     895-905, unconditional) -- also applied here rather than rejected.
--
-- Errors: 50201 slot already occupied, 50202 name already taken -- both pre-checked; the table's unique
-- constraints are the race backstop. All 5 write statements (Characters/CharacterItems x2/CharacterSkills/
-- CharacterHotkeys) run inside one explicit transaction: SET XACT_ABORT ON alone does not make a
-- multi-statement batch atomic in autocommit mode, so a mid-sequence failure (e.g. a bad @Equipment/
-- @Inventory row) must not leave a committed character row with a partial or missing starter kit.
CREATE OR ALTER PROCEDURE game.usp_Character_CreateWithStarterKit @AccountId INT,
                                                                  @Slot TINYINT,
                                                                  @Name NVARCHAR(13),
                                                                  @Tribe TINYINT,
                                                                  @Gender TINYINT,
                                                                  @HeadType TINYINT,
                                                                  @FaceType TINYINT,
                                                                  @MapId SMALLINT,
                                                                  @PosX REAL,
                                                                  @PosY REAL,
                                                                  @PosZ REAL,
                                                                  @Life INT,
                                                                  @MaxLife INT,
                                                                  @Mana INT,
                                                                  @MaxMana INT,
                                                                  @WelcomeBuffUntilDate INT,
                                                                  @PremiumUntilUnixSeconds BIGINT,
                                                                  @Equipment game.tvp_CharacterItemSlot READONLY,
                                                                  @Inventory game.tvp_CharacterItemSlot READONLY,
                                                                  @Skills game.tvp_CharacterSkillSlot READONLY,
                                                                  @Hotkeys game.tvp_CharacterHotkeySlot READONLY,
                                                                  @PreviousTribe TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, N'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, N'Character name already taken.', 1;

    DECLARE @CharacterId TABLE
                         (
                             CharacterId INT
                         );

    BEGIN TRANSACTION;

    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     ProtectForDeath, AutoTime2,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, InventoryDate, StoreDate, PremiumExpireUtc)
    OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY,
            @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 1, 0, 0, 0, 0, 50, 0,
            1314, 0, 10, 0, 99999999, 5, 1440, 300, 300, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate,
            @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

    DECLARE @NewCharacterId INT = (SELECT CharacterId FROM @CharacterId);

    INSERT INTO game.CharacterItems
    (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId,
           2,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Equipment;

    INSERT INTO game.CharacterItems
    (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @NewCharacterId,
           0,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Inventory;

    INSERT INTO game.CharacterSkills (CharacterId, SlotIndex, SkillId, Grade)
    SELECT @NewCharacterId, SlotIndex, SkillId, Grade
    FROM @Skills;

    INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2)
    SELECT @NewCharacterId, Page, KeyIndex, Sort, Value1, Value2
    FROM @Hotkeys;

    COMMIT TRANSACTION;

    SELECT @NewCharacterId AS CharacterId;
END;
