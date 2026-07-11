using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record BloodExchangeCatalogRowDto(
    int BloodExchangeSlot,
    int? ItemId,
    int Cost,
    int Quantity);

[GenerateDto]
public sealed partial record EventDefinitionRowDto(
    int EventDefinitionId,
    int EventType,
    string? SortKey,
    int Rate,
    short? ZoneNumber,
    string? Message);

[GenerateDto]
public sealed partial record ItemMallProductRowDto(
    int ItemMallProductId,
    byte ProductType,
    int? ItemId,
    int Quantity,
    int Cost,
    bool IsActive);

[GenerateDto]
public sealed partial record RewardBundleRowDto(
    int RewardBundleId);

[GenerateDto]
public sealed partial record RewardBundleItemRowDto(
    int RewardBundleId,
    byte SlotIndex,
    int? ItemId);
