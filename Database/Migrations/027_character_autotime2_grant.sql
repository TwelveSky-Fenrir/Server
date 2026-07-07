-- Starting free auto-hunt minute allowance grant (aAutoTime2): closes an open question from the prior audit
-- round (audit_2026-07-05/06 series) that flagged aAutoTime2 as "granted at creation in legacy but
-- completely unrepresented -- no column anywhere in game.Characters".
--
-- Confirmed this session, read directly (not from ServerDocs alone):
--   * Server/ts25login/S04_MyWork02.cpp:885-893 (the `#ifdef LNW33` block, same block 025/026 already cite
--     for ProtectForDeath/DoubleExpTime1/DoubleExpTime2/AutoBuffTime):
--         tAvatarInfo.aDoubleExpTime1 = 300;
--         tAvatarInfo.aDoubleExpTime2 = 300;
--         tAvatarInfo.aAutoTime2 = 1440;         <-- this migration's literal
--         tAvatarInfo.aProtectForDeath = 5;
--     LNW33 is confirmed active for the ts25login build specifically (not just ts25zone, already established
--     by 025/026): Server/ts25login/ts25login.vcxproj:66 (ZLoginPreprocessorDefinitions) ->
--     Server/ts25latest_general.props:19 (`$(Z33PreDef);TS25_LOGIN;...`) -> Server/ts25latest_config.props:6,11
--     (Z33PreDef = `M33;...` for ReleaseM33, `LNW33EU;M33;...` for ReleaseEU33 -- M33 defined in BOTH shipped
--     configurations) -> Server/Header/Protocol/DEFINE.h:21-23 (`#ifdef M33` / `#define LNW33`, checked before
--     `__GOD__`/`__REBIRTH__` on the very next two lines). So this literal-1440 grant is live in every shipped
--     build configuration, exactly like its three siblings already fixed by 025/026.
--   * By contrast, the adjacent `tAvatarInfo.aAutoTime = 99999999;` (S04_MyWork02.cpp:881-883) is gated by
--     `#ifdef FREE_HUNT`, and FREE_HUNT (DEFINE.h:48) is declared only inside the `#else` arm of the same
--     `#ifdef M33` / `#else` / `#endif` block (DEFINE.h:21-51) that defines LNW33 in its `#ifdef` arm -- i.e.
--     FREE_HUNT and LNW33 are mutually exclusive by construction, and since M33 is always defined (both
--     configs, see above), FREE_HUNT's `#else` arm never compiles. aAutoTime therefore has NO confirmed
--     creation-time grant in the shipped build; this migration deliberately does not add an AutoTime column or
--     invent a value for it -- that is a separate, explicitly open question (see this session's findings), not
--     silently folded into this fix.
--   * Runtime meaning, read directly: Server/ts25zone/S07_MyGame04.cpp:787-823, itself wrapped in
--     `#ifndef FREE_HUNT` (so -- FREE_HUNT never being defined -- this whole block DOES compile, the mirror
--     image of the aAutoTime dead-grant point above):
--         if (avt->aAutoState) {
--             if (avt->aAutoTime > 0) { if (avt->aAutoTime < ReturnNowDate()) { avt->aAutoTime = 0; ... } }
--             else if (avt->aAutoTime2 > 0) {
--                 if ((tTickCount - user->mTickCountForBotTime) >= GetMinuteFromTick(1)) {
--                     user->mTickCountForBotTime = tTickCount;
--                     avt->aAutoTime2--;
--                     if (avt->aAutoTime2 < 0) { avt->aAutoTime2 = 0; }
--                     mTRANSFER.B_AVATAR_CHANGE_INFO_2(user, S062AUTO_HUNT_HOUR, avt->aAutoTime2);
--                 }
--             }
--             if (avt->aAutoTime == 0 && avt->aAutoTime2 == 0) { mTRANSFER.B_RETURN_TO_AUTO_ZONE(mIndex); return; }
--         }
--     i.e. aAutoTime is a day-based quota compared against an absolute calendar date (ReturnNowDate()); once
--     exhausted (or never granted, per the point above), aAutoTime2 is a fallback quota that decrements by
--     exactly 1 per elapsed real minute (Server/Header/datetime.h:28-29,33: TICK_SECOND=1000ms,
--     TICK_MINUTE=60000ms, GetMinuteFromTick(1)=60000) while auto-hunt is enabled (aAutoState != 0). Once both
--     reach 0 the avatar is kicked back to its auto-hunt zone (`B_RETURN_TO_AUTO_ZONE`, S07_MyGame04.cpp:820).
--     The wire label is misleading (`S062AUTO_HUNT_HOUR`, Server/Header/Protocol/STRUCT.h:1576) but the unit
--     really is minutes, not hours -- corroborated by the two cash items that top this counter up, read
--     directly at Server/ts25zone/S04_MyWork03.cpp:5386-5407 (`case 574: //auto hunt 5hr ... tAddTime = 300;
--     wAvatar.aAutoTime2 += tAddTime;` and `case 722: tAddTime = 180; wAvatar.aAutoTime2 += tAddTime;` -- 300
--     minutes = 5 hours, 180 minutes = 3 hours, exactly matching each item's own "Nhr" name). So the literal
--     1440 granted at creation is exactly 24 hours (1440 minutes) of free auto-hunt time, usable from the very
--     first minute of auto-hunt activation since the day-based aAutoTime quota is never actually granted in
--     this build (see above) and therefore starts at 0/exhausted.
--   * Wire packing confirms no field reorder is needed on the C# side: Server/ts25login/S05_MyTransfer.cpp:
--     430-431 packs `aAutoTime` immediately followed by `aAutoTime2` -- Network/Fenrir.Network.Serialization/
--     Packets/Shared/AvatarInfo.cs already declares `AutoTime`/`AutoTime2` as adjacent `int` fields (lines
--     161-162, pre-existing, confirmed here, not newly added) in that same relative order; this migration
--     reuses that existing field rather than adding a duplicate.
--
-- Deliberately NOT in scope here (flagged as open questions this session, not silently skipped):
--   * game.Characters.AutoTime (the day-based quota) is not added -- no legacy-cited creation-time grant value
--     exists for it in this build (see the FREE_HUNT point above), so there is nothing non-speculative to
--     persist yet.
--   * The runtime per-real-minute decrement loop and the S062AUTO_HUNT_HOUR client notification
--     (S07_MyGame04.cpp:798-810) have no Game-side per-tick consumer in Fenrir yet -- this migration only
--     seeds the correct starting value so a future consumer starts from the legacy-accurate 1440, exactly the
--     same posture 026 already established for DoubleExpTime1/DoubleExpTime2's own runtime consumer.
--   * PlayerRuntimeState/CreateForRuntimeState (the in-memory zone-to-zone transfer path, as opposed to a
--     fresh DB-backed world entry) is NOT threaded with AutoTime2 here. This is a pre-existing, broader gap
--     shared with DoubleExpTime1/DoubleExpTime2/ProtectForDeath/AutoBuffTime -- none of those four are
--     projected onto PlayerRuntimeState/PlayerEnterData either (see EnterWorldService.CompleteWorldEntryAsync's
--     own PlayerEnterData construction) -- adding just AutoTime2 there would be an inconsistent partial fix of
--     a larger, separately-scoped gap, not a targeted completion of this one.
ALTER TABLE game.Characters
    ADD AutoTime2 INT NOT NULL
        CONSTRAINT DF_Characters_AutoTime2 DEFAULT 0;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_AutoTime2 CHECK (AutoTime2 >= 0);
GO

-- Identical body to 026's version (DoubleExpTime1/DoubleExpTime2 literal-300 fix preserved verbatim), plus
-- AutoTime2 added to the INSERT column list/VALUES with the literal 1440 (see this migration's header for the
-- full citation). No new stored-procedure parameter: like ProtectForDeath/DoubleExpTime1/DoubleExpTime2, the
-- granted value is a fixed literal that does not vary by tribe/previous-tribe/gender, so it is baked directly
-- into the VALUES clause next to those other constants rather than round-tripped as a parameter.
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

    -- EU33 defaults: every stat starts at 1 (not the column default of 0) -- Server/ts25login/S04_MyWork02.cpp:
    -- 740-751, set unconditionally before the PreviousTribe/race switch, identical for every tribe/previous-
    -- tribe/gender. Every new character is also granted the same starter pet growth/activity (PetGrowth
    -- 640000000 = designer-facing "200%", PetActivity 100 = MAX_PAT_ACTIVITY_SIZE/full activity --
    -- Server/ts25login/S04_MyWork02.cpp:1132-1135, Server/Header/Protocol/DEFINE.h:612), the same starting
    -- level/rebirth/experience/stat-skill-point/mount grant (Server/ts25login/S04_MyWork02.cpp:1100-1179,
    -- forced on by Migrations/015's own USE_CUSTOME_CREATE citation), the same starting death-protection
    -- allowance (ProtectForDeath = 5, Server/ts25login/S04_MyWork02.cpp:885-889, Migrations/025's own
    -- citation), the same welcome-buff counters (DoubleExpTime1/DoubleExpTime2 = bare literal 300,
    -- Server/ts25login/S04_MyWork02.cpp:886-887, Migrations/026's own citation), and the same starting free
    -- auto-hunt minute allowance (AutoTime2 = bare literal 1440 = 24h worth of minutes,
    -- Server/ts25login/S04_MyWork02.cpp:888, this migration's own citation) regardless of tribe -- none of
    -- these literal values vary by race/tribe/gender. The pet/cape item rows themselves travel in via
    -- @Equipment (added by CreateAvatarService.BuildEquipmentRows alongside the tribe's elite armor/gloves/
    -- boots/ring/amulet/weapon) -- neither is a world.StarterKitEquipment catalog row, both are C# constants.
    INSERT INTO game.Characters
    (AccountId, Slot, Name, Tribe, PreviousTribe, Gender, HeadType, FaceType,
     MapId, PosX, PosY, PosZ, Life, MaxLife, Mana, MaxMana,
     StatVit, StatStr, StatInt, StatDex,
     PetGrowth, PetActivity,
     Level, Level2, RebirthCount, Experience, Exp2,
     StatPoints, SkillPoints,
     MountItemId, MountExpActivity, MountPower, MountSlotIndex, MountTime,
     ProtectForDeath, AutoTime2,
     DoubleExpTime1, DoubleExpTime2, AutoBuffTime, PremiumExpireUtc)
    OUTPUT INSERTED.CharacterId INTO @CharacterId
    VALUES (@AccountId, @Slot, @Name, @Tribe, @PreviousTribe, @Gender, @HeadType, @FaceType, @MapId, @PosX, @PosY,
            @PosZ, @Life, @MaxLife, @Mana, @MaxMana, 1, 1, 1, 1, 640000000, 100, 145, 12, 0, 2000000000, 0, 3175, 10000,
            1301, 0, 5, 0, 99999999, 5, 1440, 300, 300, @WelcomeBuffUntilDate, @PremiumUntilUnixSeconds);

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

-- Identical body to 018's version (the only prior CREATE OR ALTER of this procedure), plus c.AutoTime2
-- appended at the very end of RS0's SELECT list (after c.MountTime) -- never inserted mid-list, so
-- CharacterWorldEntryDto's ordinal-mapped narrow prefix and CharacterWorldSnapshotDto's existing field order
-- both stay unaffected; only CharacterWorldSnapshotDto gains the new trailing AutoTime2 field (defaulted in
-- its C# record so existing direct-construction call sites, e.g. EnterWorldServiceTests, keep compiling).
CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence,
           c.Experience,
           c.Level2,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.Money,
           c.BigMoney,
           c.StoreMoney,
           c.BigStoreMoney,
           c.RebirthCount,
           c.Title,
           c.Halo,
           c.ContributionPoints,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForDeath,
           c.ProtectForDestroy,
           c.DoubleExpTime1,
           c.DoubleExpTime2,
           c.DropItemTime,
           c.InventoryDate,
           c.StoreDate,
           ISNULL(q.StepPermanent, 0) AS QuestStepPermanent,
           ISNULL(q.ActiveQuestId, 0) AS QuestActiveId,
           ISNULL(q.QSort, 0)         AS QuestSort,
           ISNULL(q.TargetPhase, 0)   AS QuestTargetPhase,
           ISNULL(q.KillCounter, 0)   AS QuestKillCounter,
           c.JoinWar,
           c.MissionKillOtherTribe,
           c.MissionKillMonster,
           c.MissionPlayTime,
           c.AutoHuntEnabled,
           c.AutoHuntConfig,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.PetGrowth,
           c.PetActivity,
           c.TeacherPoint,
           c.AutoBuffTime,
           c.PremiumExpireUtc,
           c.Exp2,
           c.PreviousTribe,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AutoTime2
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
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
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
    ORDER BY Container, Slot;

    SELECT SlotIndex,
           SkillId,
           Grade
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;

    SELECT Page,
           KeyIndex,
           Sort,
           Value1,
           Value2
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
    ORDER BY Page, KeyIndex;

    SELECT SlotIndex,
           Value,
           RemainingLegacyTicks
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
GO
