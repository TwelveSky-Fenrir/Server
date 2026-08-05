using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Domain.Game.GameData;

public readonly record struct ItemBucketKey(short Level, byte Type, byte Sort);

public readonly record struct MartialItemBucketKey(short Level, byte Type, byte Sort, byte MartialLevel);

public sealed class WorldDataCache
{
    private WorldDataCache? _publishedSnapshot;

    private FrozenDictionary<int, ItemDefinition> _itemsById = null!;
    private FrozenDictionary<ItemBucketKey, ImmutableArray<ItemRowDto>> _itemSearchBuckets = null!;
    private FrozenDictionary<MartialItemBucketKey, ImmutableArray<ItemRowDto>> _martialItemSearchBuckets = null!;
    private FrozenDictionary<int, SkillDefinition> _skillsById = null!;
    private FrozenDictionary<int, MonsterDefinition> _monstersById = null!;
    private FrozenDictionary<int, NpcDefinition> _npcsById = null!;
    private FrozenDictionary<int, QuestDefinition> _questsById = null!;
    private FrozenDictionary<short, LevelRowDto> _levelsByLevel = null!;
    private FrozenDictionary<short, ZoneDefinition> _zonesByNumber = null!;
    private FrozenDictionary<int, GemSocketRowDto> _gemSocketsById = null!;
    private FrozenDictionary<int, GemSocketRowDto> _gemSocketsByTypeAndValue = null!;
    private ImmutableArray<BloodExchangeCatalogRowDto> _bloodExchangeCatalog;
    private ImmutableArray<EventDefinitionRowDto> _eventDefinitions;
    private FrozenDictionary<int, ItemMallProductRowDto> _itemMallProductsById = null!;
    private FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> _rewardBundleItemsByBundleId = null!;
    private CashCatalogBuilder.CashCatalog _cashCatalog = null!;
    private int _cashCatalogVersion;

    private WorldDataCache Snapshot => Volatile.Read(ref _publishedSnapshot) ?? this;

    public WorldDataCache Capture()
    {
        return Snapshot;
    }

    public required FrozenDictionary<int, ItemDefinition> ItemsById
    {
        get => Snapshot._itemsById;
        init => _itemsById = value;
    }

    public required FrozenDictionary<ItemBucketKey, ImmutableArray<ItemRowDto>> ItemSearchBuckets
    {
        get => Snapshot._itemSearchBuckets;
        init => _itemSearchBuckets = value;
    }

    public required FrozenDictionary<MartialItemBucketKey, ImmutableArray<ItemRowDto>> MartialItemSearchBuckets
    {
        get => Snapshot._martialItemSearchBuckets;
        init => _martialItemSearchBuckets = value;
    }

    public required FrozenDictionary<int, SkillDefinition> SkillsById
    {
        get => Snapshot._skillsById;
        init => _skillsById = value;
    }

    public required FrozenDictionary<int, MonsterDefinition> MonstersById
    {
        get => Snapshot._monstersById;
        init => _monstersById = value;
    }

    public required FrozenDictionary<int, NpcDefinition> NpcsById
    {
        get => Snapshot._npcsById;
        init => _npcsById = value;
    }

    public required FrozenDictionary<int, QuestDefinition> QuestsById
    {
        get => Snapshot._questsById;
        init => _questsById = value;
    }

    public required FrozenDictionary<short, LevelRowDto> LevelsByLevel
    {
        get => Snapshot._levelsByLevel;
        init => _levelsByLevel = value;
    }

    public required FrozenDictionary<short, ZoneDefinition> ZonesByNumber
    {
        get => Snapshot._zonesByNumber;
        init => _zonesByNumber = value;
    }

    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsById
    {
        get => Snapshot._gemSocketsById;
        init => _gemSocketsById = value;
    }

    public required FrozenDictionary<int, GemSocketRowDto> GemSocketsByTypeAndValue
    {
        get => Snapshot._gemSocketsByTypeAndValue;
        init => _gemSocketsByTypeAndValue = value;
    }

    public required ImmutableArray<BloodExchangeCatalogRowDto> BloodExchangeCatalog
    {
        get => Snapshot._bloodExchangeCatalog;
        init => _bloodExchangeCatalog = value;
    }

    public required ImmutableArray<EventDefinitionRowDto> EventDefinitions
    {
        get => Snapshot._eventDefinitions;
        init => _eventDefinitions = value;
    }

    public required FrozenDictionary<int, ItemMallProductRowDto> ItemMallProductsById
    {
        get => Snapshot._itemMallProductsById;
        init => _itemMallProductsById = value;
    }

    public required FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> RewardBundleItemsByBundleId
    {
        get => Snapshot._rewardBundleItemsByBundleId;
        init => _rewardBundleItemsByBundleId = value;
    }

    public required CashCatalogBuilder.CashCatalog CashCatalog
    {
        get => Snapshot._cashCatalog;
        init => _cashCatalog = value;
    }

    public required int CashCatalogVersion
    {
        get => Snapshot._cashCatalogVersion;
        init => _cashCatalogVersion = value;
    }

    public void Publish(WorldDataCache snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (ReferenceEquals(snapshot, this))
            throw new ArgumentException("A world-data cache cannot publish itself.", nameof(snapshot));

        Volatile.Write(ref _publishedSnapshot, snapshot);
    }
}
