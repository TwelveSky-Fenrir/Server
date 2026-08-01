using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Domain.Game.GameData;

public sealed class WorldDataCache
{
    public required FrozenDictionary<int, ItemDefinition> ItemsById { get; init; }

    public required FrozenDictionary<int, SkillDefinition> SkillsById { get; init; }

    public required FrozenDictionary<int, MonsterDefinition> MonstersById { get; init; }

    public required FrozenDictionary<int, NpcDefinition> NpcsById { get; init; }

    public required FrozenDictionary<int, QuestDefinition> QuestsById { get; init; }

    public required FrozenDictionary<short, LevelRowDto> LevelsByLevel { get; init; }

    public required FrozenDictionary<short, ZoneDefinition> ZonesByNumber { get; init; }

    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsById { get; init; }

    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsByTypeAndValue { get; init; }

    public required ImmutableArray<BloodExchangeCatalogRowDto> BloodExchangeCatalog { get; init; }

    public required ImmutableArray<EventDefinitionRowDto> EventDefinitions { get; init; }

    public required FrozenDictionary<int, ItemMallProductRowDto> ItemMallProductsById { get; init; }

    public required FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> RewardBundleItemsByBundleId
    {
        get;
        init;
    }

    public required CashCatalogBuilder.CashCatalog CashCatalog { get; init; }

    public required int CashCatalogVersion { get; init; }
}
