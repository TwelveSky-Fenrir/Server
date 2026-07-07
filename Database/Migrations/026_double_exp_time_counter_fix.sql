-- Corrective follow-up to Migrations/025_character_protect_for_death_grant.sql (itself layered on 018 --
-- never edited in place, DbMigrator journals every script by content hash and refuses a changed re-apply).
-- Builds on 025's ProtectForDeath-grant body (not 018's) so this migration does not silently revert that
-- sibling fix -- both migrations touch the same CREATE OR ALTER PROCEDURE, and whichever applies last wins
-- the full procedure body, so each corrective migration in this chain must be based on the immediately
-- preceding one, not re-derived from an older ancestor.
--
-- Bug: usp_Character_CreateWithStarterKit persists DoubleExpTime1, DoubleExpTime2, AND AutoBuffTime as the
-- identical @WelcomeBuffUntilDate value (a genuine YYYYMMDD-encoded date, ~7 days out, e.g. 20260714). A
-- fresh legacy re-audit (behavior contract: create-avatar-tribe-handling-fresh) found this is confirmed
-- wrong by orders of magnitude, not merely imprecise:
--
--   * Server/ts25login/S04_MyWork02.cpp:885-892 sets `aDoubleExpTime1 = 300;` and `aDoubleExpTime2 = 300;`
--     as bare integer literals, three lines before the wholly separate `aAutoBuffTime =
--     ReturnAddDate(0, 7)` -- three independent assignments, not one value applied three times.
--   * Server/ts25zone/S07_MyGame04.cpp:955-969 proves DoubleExpTime1/DoubleExpTime2 are raw per-tick
--     decrementing counters: a periodic per-connected-avatar handler does
--     `if (avt->aDoubleExpTime1 > 0) { avt->aDoubleExpTime1--; ...notify client (S017DOUBLE_EXP_TIME)... }`
--     and the identical pattern for aDoubleExpTime2 (S043DOUBLE_EXP_TIME2) -- never compared against a
--     calendar date anywhere. Persisting a date-shaped value (order of 20 million) into a field consumed by
--     this decrement-by-1-per-firing logic would take ~20 million firings to reach zero instead of 300 --
--     the welcome-buff grant would last many orders of magnitude longer than the legacy 300-tick grant.
--
-- Fix: identical body to 025's version (ProtectForDeath grant preserved verbatim), with the
-- DoubleExpTime1/DoubleExpTime2 columns in the INSERT's VALUES list changed from @WelcomeBuffUntilDate to
-- the literal 300 -- matching the legacy bare-integer-literal assignment exactly, and consistent with how
-- every other fixed EU33 creation-time constant here (StatVit/Str/Int/Dex, PetGrowth/PetActivity, Level/
-- Level2/RebirthCount/Experience/Exp2, StatPoints/SkillPoints, Mount*, ProtectForDeath) is already baked
-- directly into this same VALUES list rather than round-tripped as a parameter. No new @Parameter is
-- introduced: the only signature-visible change is that @WelcomeBuffUntilDate now solely drives AutoBuffTime.
-- AutoBuffTime itself is untouched (still @WelcomeBuffUntilDate) -- it is the one genuinely date-shaped
-- field of the three.
--
-- Deliberately NOT in scope here (see the behavior contract's own open questions, not guessed at):
--   * The real-world wall-clock duration one throttle firing represents (contract flags this unconfirmed).
--   * The runtime decrement-and-notify consumer itself (Server/ts25zone/S07_MyGame04.cpp:889-930,955-969) --
--     no Game-side per-tick consumer of DoubleExpTime1/DoubleExpTime2 exists in Fenrir yet; this migration
--     only corrects the creation-time seed value so a future consumer starts from the legacy-accurate 300,
--     not a ~20-million date.
--   * Tables/game/Characters.sql's own column comments ("YYYYMMDD, 0 = no boost" on DoubleExpTime1/2) are
--     now stale/misleading given this finding, but that script is already applied (referenced in
--     _manifest.txt since before this migration) -- DbMigrator refuses a changed re-apply of an
--     already-journaled script, so the comment is left as-is here rather than edited in place. The column
--     type itself (INT) is unaffected either way -- 300 fits it exactly as well as a YYYYMMDD value did.
CREATE OR ALTER PROCEDURE game.usp_Character_CreateWithStarterKit
    @AccountId INT,
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

    DECLARE @CharacterId TABLE (CharacterId INT);

    BEGIN TRANSACTION;

    -- EU33 defaults: every stat starts at 1 (not the column default of 0) -- Server/ts25login/S04_MyWork02.cpp:
    -- 740-751, set unconditionally before the PreviousTribe/race switch, identical for every tribe/previous-
    -- tribe/gender. Every new character is also granted the same starter pet growth/activity (PetGrowth
    -- 640000000 = designer-facing "200%", PetActivity 100 = MAX_PAT_ACTIVITY_SIZE/full activity --
    -- Server/ts25login/S04_MyWork02.cpp:1132-1135, Server/Header/Protocol/DEFINE.h:612), the same starting
    -- level/rebirth/experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179,
    -- forced on by Migrations/015's own USE_CUSTOME_CREATE citation), the same starting death-protection
    -- allowance (ProtectForDeath = 5, Server/ts25login/S04_MyWork02.cpp:885-889, Migrations/025's own
    -- citation), and the same welcome-buff counters (DoubleExpTime1/DoubleExpTime2 = bare literal 300,
    -- Server/ts25login/S04_MyWork02.cpp:886-887 -- genuinely independent of AutoBuffTime's date below, see
    -- this migration's own header) regardless of tribe -- none of these literal values vary by
    -- race/tribe/gender. The pet/cape item rows themselves travel in via @Equipment (added by
    -- CreateAvatarService.BuildEquipmentRows alongside the tribe's elite armor/gloves/boots/ring/amulet/
    -- weapon) -- neither is a world.StarterKitEquipment catalog row, both are C# constants.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     ProtectForDeath,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
        OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES
        (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY, @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000, 1301, 0, 5, 0, 99999999, 5, 300, 300, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

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
GO
