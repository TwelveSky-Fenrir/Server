-- CONFIRMED PRODUCT DECISION (explicit user instruction for the character-creation-level1-redesign
-- workflow, NOT a legacy-parity bug fix): a freshly created character must start at Level 1 with only a
-- basic weapon + torso/chest armor piece equipped, instead of the EU33/USE_CUSTOME_CREATE instant-elite
-- grant the original usp_Character_CreateWithStarterKit (StoredProcedures/game/
-- usp_Character_CreateWithStarterKit.sql, journaled/already-applied -- never edited in place, see that
-- script's own header) hardcodes. That original grant IS real, heavily cited, and was a deliberate,
-- correctly-implemented EU33 wire-parity choice at the time (Server/ts25login/S04_MyWork02.cpp:740-1179,
-- the USE_CUSTOME_CREATE branch force-defined unconditionally in every build configuration) -- this
-- migration is a deliberate DEPARTURE from that parity, not a correction of a mistranscribed literal.
--
-- CREATE OR ALTER keeps @AccountId..@PreviousTribe's parameter list byte-for-byte identical to the base
-- script (Fenrir.Data.Characters.CharacterRepository.CreateWithStarterKitAsync's call site is unchanged by
-- this migration) -- only the single INSERT VALUES row changes:
--   * StatVit/StatStr/StatInt/StatDex stay at 1 (already the correct Level-1 floor, untouched).
--   * PetGrowth/PetActivity -> 0/0 (game.Characters' own column DEFAULTs): no pet is ever granted by the
--     redesigned equipment set (see CreateAvatarService.BuildEquipmentRows), so the two pet-growth counters
--     would otherwise be non-zero data on a character that can never actually have a pet.
--   * Level/Level2/RebirthCount/Experience/Exp2 -> 1/0/0/0/0: a genuine Level 1 character, zero experience,
--     zero rebirths, no post-cap "high level" ladder progress.
--   * StatPoints/SkillPoints -> 50/0: Fenrir product defaults, NOT legacy-cited -- no compiled non-
--     USE_CUSTOME_CREATE branch exists anywhere in the reviewed Server/ts25login source to draw a level-1
--     starting pool from (see CreateAvatarService.StartingStatPoint's own remarks for the full reasoning,
--     including why 50 was chosen and Application/Fenrir.Application.Game.Stats/LevelProgressionCalculator.
--     cs:59-63's level-1-to-2 5-point award was used only as a loose anchor, not a mandate). SkillPoints
--     starts at 0: the starter kit already grants every starting skill directly via game.CharacterSkills
--     (BuildSkillRows/world.StarterKitSkills), so no separate spendable pool is needed at Level 1.
--   * MountItemId/MountExpActivity/MountPower/MountSlotIndex/MountTime -> 0/0/0/-1/0 (all four already
--     game.Characters' own column DEFAULTs; MountSlotIndex's DEFAULT is specifically -1, "no mount active",
--     matching DF_Characters_MountSlotIndex): no starter mount is granted anymore.
--   * ProtectForDeath/AutoTime2 -> 0/0 (column DEFAULTs): these were the LNW33 "instant boost" grants
--     (death-protection shield charges, free auto-hunt minutes) -- inconsistent with a fresh Level 1 start,
--     dropped entirely rather than granted at a smaller value.
--   * DoubleExpTime1/DoubleExpTime2 -> 0/0 (column DEFAULTs): same "instant boost" treatment as
--     ProtectForDeath/AutoTime2 above -- the raw per-tick double-XP counters no longer start pre-charged.
--   * AutoBuffTime/InventoryDate/StoreDate: UNCHANGED, still @WelcomeBuffUntilDate (today + 7 days) -- the
--     welcome-buff/second-inventory-page/second-store-page rental grant is independent of the EU33 instant-
--     elite block and is kept as-is (see CreateAvatarService's own remarks for why this one grant survives
--     the redesign).
--   * PremiumExpireUtc -> 0 (column DEFAULT, "0 = none"): no premium-day grant anymore. @PremiumUntilUnixSeconds
--     stays a declared parameter (signature parity) but is now DELIBERATELY UNUSED in the INSERT --
--     CreateAvatarService.cs passes a fixed 0 for it going forward; kept as a no-op parameter rather than
--     removed so CharacterRepository.CreateWithStarterKitAsync's call shape (and every other existing
--     caller of that method) needs no signature change for this workflow's LoginServer-only scope.
--
-- Errors/transaction shape unchanged from the base script (50201/50202 pre-checked THROWs; all 5 writes in
-- one explicit transaction under SET XACT_ABORT ON) -- only the literal INSERT VALUES row differs.
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
        THROW 50201, 'Character slot already occupied for this account.', 1;

    IF EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, 'Character name already taken.', 1;

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
            @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 50, 0,
            0, 0, 0, -1, 0, 0, 0, 0, 0, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate,
            @WelcomeBuffUntilDate, 0);

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
