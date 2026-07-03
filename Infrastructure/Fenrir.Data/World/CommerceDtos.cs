using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.BloodExchangeCatalog row -- ordinal contract of world.usp_BloodExchangeCatalog_GetAll
///     (3 real rows). ItemId is NULL for a slot with no item wired up.
/// </summary>
[GenerateDto]
public sealed partial record BloodExchangeCatalogRowDto(
    int BloodExchangeSlot,
    int? ItemId,
    int Cost,
    int Quantity);

/// <summary>
///     One world.EventDefinitions row -- ordinal contract of world.usp_EventDefinition_GetAll (0 rows in the
///     legacy dump; loaded anyway so future GM tooling has a stable boot-time read path from day one).
/// </summary>
[GenerateDto]
public sealed partial record EventDefinitionRowDto(
    int EventDefinitionId,
    int EventType,
    string? SortKey,
    int Rate,
    short? ZoneNumber,
    string? Message);

/// <summary>
///     One world.ItemMallProducts row -- ordinal contract of world.usp_ItemMallProduct_GetAll (active AND
///     inactive; the cache keeps both so cash-shop lookups can distinguish "unknown product" from "disabled
///     product"). ItemId is NULL for a product with no item wired up.
/// </summary>
[GenerateDto]
public sealed partial record ItemMallProductRowDto(
    int ItemMallProductId,
    byte ProductType,
    int? ItemId,
    int Quantity,
    int Cost,
    bool IsActive);

/// <summary>One world.RewardBundles row -- ordinal contract of world.usp_RewardBundle_GetAll (1 row in this build).</summary>
[GenerateDto]
public sealed partial record RewardBundleRowDto(
    int RewardBundleId);

/// <summary>
///     One populated world.RewardBundleItems slot -- ordinal contract of world.usp_RewardBundleItem_GetAll
///     (SlotIndex 1-7). ItemId is NULL for a populated slot with no item wired up.
/// </summary>
[GenerateDto]
public sealed partial record RewardBundleItemRowDto(
    int RewardBundleId,
    byte SlotIndex,
    int? ItemId);
