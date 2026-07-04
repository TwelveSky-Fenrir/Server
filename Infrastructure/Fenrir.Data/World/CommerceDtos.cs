using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>world.usp_BloodExchangeCatalog_GetAll; ItemId null for a slot with no item wired up.</summary>
[GenerateDto]
public sealed partial record BloodExchangeCatalogRowDto(
    int BloodExchangeSlot,
    int? ItemId,
    int Cost,
    int Quantity);

/// <summary>world.usp_EventDefinition_GetAll; 0 rows today but loaded so future GM tooling has a stable read path.</summary>
[GenerateDto]
public sealed partial record EventDefinitionRowDto(
    int EventDefinitionId,
    int EventType,
    string? SortKey,
    int Rate,
    short? ZoneNumber,
    string? Message);

/// <summary>world.usp_ItemMallProduct_GetAll; includes inactive rows so lookups can tell "unknown product" from "disabled". ItemId null when unwired.</summary>
[GenerateDto]
public sealed partial record ItemMallProductRowDto(
    int ItemMallProductId,
    byte ProductType,
    int? ItemId,
    int Quantity,
    int Cost,
    bool IsActive);

/// <summary>world.usp_RewardBundle_GetAll (1 row in this build).</summary>
[GenerateDto]
public sealed partial record RewardBundleRowDto(
    int RewardBundleId);

/// <summary>world.usp_RewardBundleItem_GetAll (SlotIndex 1-7); ItemId null for a slot with no item wired up.</summary>
[GenerateDto]
public sealed partial record RewardBundleItemRowDto(
    int RewardBundleId,
    byte SlotIndex,
    int? ItemId);
