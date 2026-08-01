using System.Collections.ObjectModel;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateDto]
public sealed partial record StarterKitEquipmentRowDto(byte EquipSlot, int ItemId, byte? RawWeaponCode);

[GenerateDto]
public sealed partial record StarterKitInventoryRowDto(byte SlotIndex, int ItemId, int Quantity);

[GenerateDto]
public sealed partial record StarterKitSkillRowDto(byte SlotIndex, int SkillId, int Grade);

[GenerateDto]
public sealed partial record StarterKitHotkeyRowDto(byte Page, byte KeyIndex, int Sort, int Value1, int Value2);

[GenerateDto]
public sealed partial record StarterKitSpawnRowDto(float PosX, float PosY, float PosZ);

public sealed record StarterKitBundle(
    ReadOnlyCollection<StarterKitEquipmentRowDto> Equipment,
    ReadOnlyCollection<StarterKitInventoryRowDto> Inventory,
    ReadOnlyCollection<StarterKitSkillRowDto> Skills,
    ReadOnlyCollection<StarterKitHotkeyRowDto> Hotkeys,
    StarterKitSpawnRowDto? Spawn);
