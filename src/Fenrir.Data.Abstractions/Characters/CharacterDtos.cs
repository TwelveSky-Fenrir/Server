using System.Collections.ObjectModel;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateDto]
public sealed partial record CharacterIdDto(int CharacterId);

[GenerateDto]
public sealed partial record CharacterItemIdDto(int ItemId);

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

[GenerateDto]
public sealed partial record CharacterRosterDto(
    int CharacterId,
    byte Slot,
    string Name,
    byte Tribe,
    byte PreviousTribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short Level,
    short Level2,
    int Halo,
    byte RebirthCount,
    int ContributionPoints,
    int SkillPoints,
    short EatLifePotion,
    short EatManaPotion,
    short EatStrPotion,
    short EatDexPotion,
    short EatElePotion,
    int PetGrowth,
    byte PetActivity,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    int Life,
    int Mana,
    byte VisibleState = 1,
    byte SpecialState = 0,
    int CostumeIndex = -1);

[GenerateDto]
public sealed partial record CharacterRosterItemDto(
    int CharacterId,
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

[GenerateDto]
public sealed partial record CharacterRosterPetBagSlotDto(int CharacterId, byte Slot, int ItemId);

[GenerateDto]
public sealed partial record CharacterRosterCostumeSlotDto(int CharacterId, byte Slot, int ItemId);

public sealed record CharacterAccountRosterBundle(
    ReadOnlyCollection<CharacterRosterDto> Characters,
    ReadOnlyCollection<CharacterRosterItemDto> Items,
    ReadOnlyCollection<CharacterRosterPetBagSlotDto>? PetBagSlots = null,
    ReadOnlyCollection<CharacterRosterCostumeSlotDto>? CostumeSlots = null);

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
    byte RebirthCount,
    int Title,
    int Halo,
    int ContributionPoints,
    short EatLifePotion,
    short EatManaPotion,
    short EatStrPotion,
    short EatDexPotion,
    short EatElePotion,
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
    byte[] AutoHuntConfig,
    byte AutoLifeRatio,
    byte AutoManaRatio,
    int PetGrowth,
    byte PetActivity,
    int TeacherPoint,
    int AutoBuffTime,
    long PremiumExpireUtc,
    int Exp2,
    byte PreviousTribe,
    int MountItemId,
    int MountExpActivity,
    int MountPower,
    int MountSlotIndex,
    int MountTime,
    int AutoTime2 = 0,
    int Zone241Time = 0,
    int PetBagDate = 0,
    int WarPoint = 0,
    byte M15PetLuckyBoxPity = 0);

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

[GenerateDto]
public sealed partial record CharacterSkillDto(
    byte SlotIndex,
    int SkillId,
    int Grade);

[GenerateDto]
public sealed partial record CharacterHotkeyDto(
    byte Page,
    byte KeyIndex,
    int Sort,
    int Value1,
    int Value2);

[GenerateDto]
public sealed partial record CharacterBuffDto(
    byte SlotIndex,
    int Value,
    int RemainingLegacyTicks);

public sealed record CharacterWorldEntryBundle(
    CharacterWorldSnapshotDto Character,
    ReadOnlyCollection<CharacterItemSlotDto> Items,
    ReadOnlyCollection<CharacterSkillDto> Skills,
    ReadOnlyCollection<CharacterHotkeyDto> Hotkeys,
    ReadOnlyCollection<CharacterBuffDto> Buffs);
