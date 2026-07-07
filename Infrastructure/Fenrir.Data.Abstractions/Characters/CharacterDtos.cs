using System.Collections.ObjectModel;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>One-row result of usp_Character_GetIdByName -- resolves an avatar name to its CharacterId.</summary>
[GenerateDto]
public sealed partial record CharacterIdDto(int CharacterId);

/// <summary>
///     One-row result of usp_CharacterItem_GetIdAtSlot -- the ItemId currently occupying one (Container, Slot)
///     pair. Row-absent (mapped to a null return by the repository) means the slot is empty.
/// </summary>
[GenerateDto]
public sealed partial record CharacterItemIdDto(int ItemId);

// game.usp_Character_GetByAccount; ordinal-mapped, ctor order must match the SELECT.
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

// game.usp_Character_GetForWorldEntry; drives ZC_REGISTER_AVATAR_RECV/AVATAR_INFO. Mirrors game.Characters minus audit timestamps.
// Deliberately does NOT carry PreviousTribe/Mount* (added to CharacterWorldSnapshotDto by
// Migrations/018_character_previous_tribe_and_mount_readpath.sql): this type's only current consumers
// (CreateAvatarService's create-response overlay, ZoneTransferService's shard routing/vitals clamp) don't
// need either, and both new fields are appended past this record's read range in RS0 for exactly that
// reason -- extend this record too, in the same append-only fashion, the day a consumer actually needs it.
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

// RS0 of usp_Character_GetForWorldEntry: CharacterWorldEntryDto's exact prefix (kept stable for existing callers) plus appended progression + quest state (legacy inline avatar state, hence same result set not a 6th).
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
    // Bare byte[] (not byte[]?): [GenerateDto] only recognizes bare byte[] for varbinary mapping; NULL still maps fine at runtime.
    byte[] AutoHuntConfig,
    byte AutoLifeRatio,
    byte AutoManaRatio,
    int PetGrowth,
    byte PetActivity,
    // wAvatar.aTeacherPoint; appended last to match the proc's column order.
    int TeacherPoint,
    // aAutoBuffTime/aPremium, both appended after TeacherPoint for the same reason -- neither is read into
    // PlayerRuntimeState yet (see EnterWorldHandler), same "durably stored, runtime wiring pending" posture
    // DoubleExpTime1/2 already have above.
    int AutoBuffTime,
    long PremiumExpireUtc,
    // wAvatar.aExp2; appended last for the same reason AutoBuffTime/PremiumExpireUtc were -- see TribeActionHandler.HandleRebirthAsync for the one live consumer.
    int Exp2,
    // Migrations/018_character_previous_tribe_and_mount_readpath.sql: appended after Exp2, not inserted next
    // to Tribe, so CharacterWorldEntryDto's ordinal-mapped prefix (this record's own first 19 fields) never
    // shifts. PreviousTribe is the Noble Dragon/Royal Serpent/Grand Tiger starter-kit template (0-2) --
    // genuinely independent of Tribe, see Server/ts25zone/S04_MyWork02.cpp:880-901's self-consistency check.
    byte PreviousTribe,
    // The 5 columns Migrations/015_starter_kit_elite_grant.sql granted at creation
    // (Server/ts25login/S04_MyWork02.cpp:1174-1179) but no procedure/DTO projected until now -- see this
    // migration's own header for the full "inert data" citation.
    int MountItemId,
    int MountExpActivity,
    int MountPower,
    int MountSlotIndex,
    int MountTime,
    // Migrations/027_character_autotime2_grant.sql: aAutoTime2, the free auto-hunt minute allowance
    // (Server/ts25login/S04_MyWork02.cpp:888 grants the literal 1440 == 24h of minutes at creation;
    // Server/ts25zone/S07_MyGame04.cpp:787-823 decrements it by 1 per elapsed real minute while auto-hunt is
    // enabled). Appended last, defaulted to 0 (same "pre-existing N-arg test construction keeps compiling"
    // posture as AccountDtos.AuthenticateAccountDto.AccountGrade), not inserted next to MountTime, so this
    // record's field order for every prior column is unchanged.
    int AutoTime2 = 0);

/// <summary>
///     RS1 of usp_Character_GetForWorldEntry. ExpireDate: legacy YYYYMMDD int, 0 = not a rental. Container: 0/1
///     inventory pages, 2 equipment, 3 store.
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

/// <summary>RS2 of usp_Character_GetForWorldEntry -- legacy aSkill[slot][0..1].</summary>
[GenerateDto]
public sealed partial record CharacterSkillDto(
    byte SlotIndex,
    int SkillId,
    int Grade);

/// <summary>RS3 -- legacy aHotKey[page][key][0..2] stored verbatim; per-sort semantics live in the hotkey logic, not here.</summary>
[GenerateDto]
public sealed partial record CharacterHotkeyDto(
    byte Page,
    byte KeyIndex,
    int Sort,
    int Value1,
    int Value2);

/// <summary>RS4; RemainingLegacyTicks is 500ms legacy ticks, persisted verbatim (no ms conversion at this boundary).</summary>
[GenerateDto]
public sealed partial record CharacterBuffDto(
    byte SlotIndex,
    int Value,
    int RemainingLegacyTicks);

/// <summary>
///     All five result sets of usp_Character_GetForWorldEntry, stitched -- not a [GenerateDto] since it spans result
///     sets.
/// </summary>
public sealed record CharacterWorldEntryBundle(
    CharacterWorldSnapshotDto Character,
    ReadOnlyCollection<CharacterItemSlotDto> Items,
    ReadOnlyCollection<CharacterSkillDto> Skills,
    ReadOnlyCollection<CharacterHotkeyDto> Hotkeys,
    ReadOnlyCollection<CharacterBuffDto> Buffs);
