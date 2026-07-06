-- op17 CL_CREATE_AVATAR_SEND2's full creation path: usp_Character_Create's slot/name guards plus the EU33
-- starter kit in the same transaction (stats, pet, welcome buffs, premium day, and the tribe's equipment/
-- inventory/skills/hotkeys). Left as a separate proc from usp_Character_Create rather than extending it in
-- place: that proc is still the minimal-dependency character factory every other feature's test suite uses
-- to satisfy game.Characters' FK, and giving every one of those call sites 6 new required parameters (plus
-- 4 TVPs) would be unrelated churn.
-- Errors: 50201 slot already occupied, 50202 name already taken -- both pre-checked; the table's unique
-- constraints are the race backstop.
CREATE PROCEDURE game.usp_Character_CreateWithStarterKit @AccountId               INT,
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
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot)
        THROW 50201, 'Character slot already occupied for this account.', 1;

    IF
EXISTS (SELECT 1 FROM game.Characters WHERE Name = @Name)
        THROW 50202, 'Character name already taken.', 1;

    DECLARE
@CharacterId TABLE (CharacterId INT);

    -- EU33 defaults: every stat starts at 1 (not the column default of 0), and every new character is
    -- granted the same starter pet growth/activity (200% growth, full activity) regardless of tribe -- the
    -- pet/cape item rows themselves travel in via @Equipment alongside the tribe's armor/gloves/boots/weapon.
INSERT INTO game.Characters
(AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType,
 MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
 StatVit, StatStr, StatInt, StatDex,
 PetGrowth, PetActivity,
 DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
    OUTPUT INSERTED.CharacterId INTO @CharacterId
VALUES
    (@AccountId, @Slot, @Name, @Tribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

DECLARE
@NewCharacterId INT = (SELECT CharacterId FROM @CharacterId);

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

SELECT @NewCharacterId AS CharacterId;
END;
