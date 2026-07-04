-- database/50_procedures/game/usp_Character_GetForWorldEntry.sql
-- Contract: full character snapshot feeding world entry (ZC_REGISTER_AVATAR_RECV / AVATAR_INFO), ONE
-- round trip -- extended in phase A3 from the single M1 result set to five (CaeriusNet:
-- QueryMultipleReadOnlyCollectionAsync<T1..T5>; five is that API's arity ceiling, which is why the quest
-- state -- a single-row inline block in the legacy avatar struct, wAvatar.aQuestInfo[5] -- is folded
-- into RS0 via LEFT JOIN instead of being a sixth result set).
-- Params:
--   @CharacterId INT
-- RS0 (at most one row; ordinal order = [GenerateDto] contract). Columns 1-19 are the M1 contract,
-- UNCHANGED and in the same order (CharacterWorldEntryDto still maps this prefix); everything after
-- FlushSequence is appended A3 state:
--    1..19  CharacterId, AccountId, Slot, Name, Tribe, Gender, HeadType, FaceType, Level, MapId,
--           PosX, PosY, PosZ, Heading, Life, MaxLife, Mana, MaxMana, FlushSequence
--   20..47  Experience, Level2, StatVit, StatStr, StatInt, StatDex, StatPoints, SkillPoints, Money,
--           BigMoney, StoreMoney, BigStoreMoney, RebirthCount, Title, Halo, ContributionPoints,
--           EatLifePotion, EatManaPotion, EatStrPotion, EatDexPotion, EatElePotion, ProtectForDeath,
--           ProtectForDestroy, DoubleExpTime1, DoubleExpTime2, DropItemTime, InventoryDate, StoreDate
--   48..52  QuestStepPermanent, QuestActiveId, QuestSort, QuestTargetPhase, QuestKillCounter
--           (game.CharacterQuests LEFT JOIN, ISNULL 0 -- no row = the all-zeros aQuestInfo of a fresh
--           character).
--   53..62  [Server Logic V9 Progression] JoinWar, MissionKillOtherTribe, MissionKillMonster,
--           MissionPlayTime (wAvatar.aMissionDate, report 04 CZ_MISSION_COMPLETE_SEND), AutoHuntEnabled,
--           AutoHuntConfig (raw 112-byte AUTO_HUNT blob, NULL for a character that never configured
--           auto-hunt -- callers must treat NULL as "all-zeros"), AutoLifeRatio, AutoManaRatio
--           (CZ_CHANGE_AUTO_INFO), PetGrowth, PetActivity (see game.Characters.PetGrowth's own ALTER
--           header for the per-character simplification these two model).
--   63      TeacherPoint (quest reward type 5, wAvatar.aTeacherPoint -- see Characters_v9_teacherpoint.sql's
--           own ALTER header; appended LAST, matching this proc's own ordinal-mapped contract).
-- RS1 (0..n): occupied item slots -- Container, Slot, ItemId, Quantity, Enchant, Combine, Refine,
--   Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial (ORDER BY Container, Slot: wire
--   arrays are rebuilt in slot order).
-- RS2 (0..n): learned skills -- SlotIndex, SkillId, Grade (ORDER BY SlotIndex).
-- RS3 (0..n): assigned hotkeys -- Page, KeyIndex, Sort, Value1, Value2 (ORDER BY Page, KeyIndex).
-- RS4 (0..n): persisted buffs -- SlotIndex, Value, RemainingLegacyTicks (ORDER BY SlotIndex).
--   [CORRIGÉ-REVUE, décision D8, voir game.CharacterBuffs.sql] the legacy unconditionally wipes buff state
--   at every fresh login (ts25playuser RegisterUserForLogin) -- callers of THIS proc from the
--   Login-handover world-entry path (a genuinely fresh session) MUST ignore RS4 and start with an empty
--   buff state, not apply it. RS4 exists for a future crash/restart-recovery use, not for normal entry.
-- CreatedAtUtc/UpdatedAtUtc are audit-only and intentionally excluded: this is the world-entry payload
-- contract, not a row mirror.
-- Empty RS0 (and empty RS1-4) if @CharacterId does not exist (caller maps this to a null DTO/bundle).
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

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
       c.TeacherPoint
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
