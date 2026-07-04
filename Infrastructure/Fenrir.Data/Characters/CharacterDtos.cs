using System.Collections.ObjectModel;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Characters;

/// <summary>One-row result of usp_Character_GetIdByName -- resolves an avatar name to its CharacterId.</summary>
[GenerateDto]
public sealed partial record CharacterIdDto(int CharacterId);

/// <summary>
///     Character-select list row (architecture reference §11.2) -- ordinal contract of
///     game.usp_Character_GetByAccount's result set: CharacterId, Slot, Name, Tribe, Gender, HeadType, FaceType, Level.
///     Constructor order must track the SELECT column order exactly (invariant I-04); [GenerateDto] maps by position,
///     not by name.
/// </summary>
[GenerateDto]
public sealed partial record CharacterSummaryDto(
    int CharacterId,
    byte Slot,
    string Name,
    byte Tribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short Level);

/// <summary>
///     Full world-entry snapshot (M1 Legacy Wire Contract §4.4/§6.2) -- ordinal contract of
///     game.usp_Character_GetForWorldEntry's result set. Drives ZC_REGISTER_AVATAR_RECV/AVATAR_INFO; column order
///     mirrors game.Characters (minus the audit timestamps, which the world path never needs).
/// </summary>
[GenerateDto]
public sealed partial record CharacterWorldEntryDto(
    int CharacterId,
    int AccountId,
    byte Slot,
    string Name,
    byte Tribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short Level,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading,
    int Life,
    int MaxLife,
    int Mana,
    int MaxMana,
    long FlushSequence);

/// <summary>
///     Full RS0 of the A3-extended game.usp_Character_GetForWorldEntry: the M1 columns (the exact
///     <see cref="CharacterWorldEntryDto" /> prefix, kept UNCHANGED and in the same order) followed by the appended
///     progression columns and the folded-in quest state (wAvatar.aQuestInfo[5], LEFT JOIN of game.CharacterQuests --
///     inline avatar state in the legacy, hence RS0 and not a sixth result set). A separate type instead of appending
///     to CharacterWorldEntryDto: that positional record is constructed by existing callers/tests whose call sites
///     must not break, while [GenerateDto] mapping the shorter prefix of the same result set keeps both valid.
/// </summary>
[GenerateDto]
public sealed partial record CharacterWorldSnapshotDto(
    int CharacterId,
    int AccountId,
    byte Slot,
    string Name,
    byte Tribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short Level,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading,
    int Life,
    int MaxLife,
    int Mana,
    int MaxMana,
    long FlushSequence,
    long Experience,
    short Level2,
    int StatVit,
    int StatStr,
    int StatInt,
    int StatDex,
    int StatPoints,
    int SkillPoints,
    long Money,
    int BigMoney,
    long StoreMoney,
    int BigStoreMoney,
    int RebirthCount,
    int Title,
    int Halo,
    int ContributionPoints,
    int EatLifePotion,
    int EatManaPotion,
    int EatStrPotion,
    int EatDexPotion,
    int EatElePotion,
    int ProtectForDeath,
    int ProtectForDestroy,
    int DoubleExpTime1,
    int DoubleExpTime2,
    int DropItemTime,
    int InventoryDate,
    int StoreDate,
    int QuestStepPermanent,
    int QuestActiveId,
    int QuestSort,
    int QuestTargetPhase,
    int QuestKillCounter,
    int JoinWar,
    int MissionKillOtherTribe,
    int MissionKillMonster,
    int MissionPlayTime,
    bool AutoHuntEnabled,
    // Not '?'-annotated: CaeriusNet's [GenerateDto] source generator only recognizes the bare `byte[]`
    // syntax for its native varbinary mapping (CAERIUS005) -- a nullable-annotated array type still maps
    // fine at runtime (NULL rows simply populate this with a null reference), this is a compile-time-only
    // annotation difference, not a behavior change.
    byte[] AutoHuntConfig,
    byte AutoLifeRatio,
    byte AutoManaRatio,
    int PetGrowth,
    byte PetActivity,
    // Quest reward type 5 (wAvatar.aTeacherPoint) -- appended LAST, matching this proc's ordinal contract;
    // see Characters_v9_teacherpoint.sql's own ALTER header.
    int TeacherPoint);

/// <summary>
///     One occupied item slot (RS1 of usp_Character_GetForWorldEntry) -- ordinal contract: Container, Slot, ItemId,
///     Quantity, Enchant, Combine, Refine, Socket, SocketGem1..3, ExpireDate (legacy int YYYYMMDD, 0 = not a rental),
///     Serial. Container enum is documented on game.CharacterItems (0/1 = inventory pages, 2 = equipment, 3 = store).
/// </summary>
[GenerateDto]
public sealed partial record CharacterItemSlotDto(
    byte Container,
    byte Slot,
    int ItemId,
    int Quantity,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate,
    int Serial);

/// <summary>
///     One learned skill slot (RS2) -- legacy aSkill[slot][0..1]: SlotIndex, SkillId, Grade.
/// </summary>
[GenerateDto]
public sealed partial record CharacterSkillDto(
    byte SlotIndex,
    int SkillId,
    int Grade);

/// <summary>
///     One assigned hotkey (RS3) -- legacy aHotKey[page][key][0..2] stored verbatim: Page, KeyIndex, Sort, Value1,
///     Value2 (per-sort semantics belong to the hotkey logic, not the storage layer).
/// </summary>
[GenerateDto]
public sealed partial record CharacterHotkeyDto(
    byte Page,
    byte KeyIndex,
    int Sort,
    int Value1,
    int Value2);

/// <summary>
///     One persisted buff slot (RS4) -- SlotIndex, Value, RemainingLegacyTicks (500 ms legacy ticks, D4: persisted
///     verbatim so no ms/tick conversion ever happens at the storage boundary).
/// </summary>
[GenerateDto]
public sealed partial record CharacterBuffDto(
    byte SlotIndex,
    int Value,
    int RemainingLegacyTicks);

/// <summary>
///     The five result sets of the A3 usp_Character_GetForWorldEntry stitched together -- everything world entry needs
///     to rebuild PlayerRuntimeState/AVATAR_INFO in one SQL round trip. Not a [GenerateDto] (it spans result sets).
/// </summary>
public sealed record CharacterWorldEntryBundle(
    CharacterWorldSnapshotDto Character,
    ReadOnlyCollection<CharacterItemSlotDto> Items,
    ReadOnlyCollection<CharacterSkillDto> Skills,
    ReadOnlyCollection<CharacterHotkeyDto> Hotkeys,
    ReadOnlyCollection<CharacterBuffDto> Buffs);
