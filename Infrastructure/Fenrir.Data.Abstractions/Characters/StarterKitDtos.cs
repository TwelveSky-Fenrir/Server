using System.Collections.ObjectModel;
using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>RS0 of usp_StarterKit_GetByPreviousTribe -- one row per fixed equip slot, plus the 3 weapon alternatives.</summary>
[GenerateDto]
public sealed partial record StarterKitEquipmentRowDto(byte EquipSlot, int ItemId);

/// <summary>RS1 -- tribe-independent, always the same 4 rows.</summary>
[GenerateDto]
public sealed partial record StarterKitInventoryRowDto(byte SlotIndex, int ItemId, int Quantity);

/// <summary>RS2 -- the ~30 non-empty slots of the 40-slot starter skill sheet (row absence = empty slot).</summary>
[GenerateDto]
public sealed partial record StarterKitSkillRowDto(byte SlotIndex, int SkillId, int Grade);

/// <summary>RS3 -- always 3 rows (page 0, keys 0-2).</summary>
[GenerateDto]
public sealed partial record StarterKitHotkeyRowDto(byte Page, byte KeyIndex, int Sort, int Value1, int Value2);

/// <summary>RS4 -- the resolved spawn map's default landing point (world.Zones); absent only if @MapId is unseeded.</summary>
[GenerateDto]
public sealed partial record StarterKitSpawnRowDto(float PosX, float PosY, float PosZ);

/// <summary>All five result sets of usp_StarterKit_GetByPreviousTribe, stitched.</summary>
public sealed record StarterKitBundle(
    ReadOnlyCollection<StarterKitEquipmentRowDto> Equipment,
    ReadOnlyCollection<StarterKitInventoryRowDto> Inventory,
    ReadOnlyCollection<StarterKitSkillRowDto> Skills,
    ReadOnlyCollection<StarterKitHotkeyRowDto> Hotkeys,
    StarterKitSpawnRowDto? Spawn);
