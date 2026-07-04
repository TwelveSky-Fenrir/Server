using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Covers <see cref="EquipmentService" />'s bridge between the raw <see cref="ItemStack" /> Equipment
///     container and <see cref="StatCalculator" />'s input shape -- verifying the WIRING (right item resolved
///     into the right slot with the right upgrade bytes, missing catalog entries skipped, stats actually
///     change when equipment does), not re-deriving MyFactor's own formulas (already covered exhaustively by
///     <c>StatCalculatorTests</c>).
/// </summary>
public class EquipmentServiceTests
{
    private static ItemRowDto Item(int itemId, short vitality = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, 0, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, vitality, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }

    private static WorldDataCache WorldData(FrozenDictionary<int, ItemDefinition> itemsById)
    {
        return new WorldDataCache
        {
            ItemsById = itemsById,
            SkillsById = EmptyFrozen<int, SkillDefinition>(),
            MonstersById = EmptyFrozen<int, MonsterDefinition>(),
            NpcsById = EmptyFrozen<int, NpcDefinition>(),
            QuestsById = EmptyFrozen<int, QuestDefinition>(),
            LevelsByLevel = EmptyFrozen<short, LevelRowDto>(),
            ZonesByNumber = EmptyFrozen<short, ZoneDefinition>(),
            GemSocketsById = EmptyFrozen<int, GemSocketRowDto>(),
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = EmptyFrozen<int, ItemMallProductRowDto>(),
            RewardBundleItemsByBundleId = EmptyFrozen<int, ImmutableArray<RewardBundleItemRowDto>>(),
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static CharacterBaseAttributes NoBonusAttributes()
    {
        return new CharacterBaseAttributes(0, 0, 0, 0, 1, 0, 0, 0, 0);
    }

    [Fact]
    public void BuildEquippedSlots_ResolvesEachOccupiedSlotAgainstTheCatalog()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [100] = new(Item(100, 50), ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();

        var container = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(7, new ItemStack(100, 1, 5, 3, 0, 0, 0, 0, 0, 0, 0));

        var slots = EquipmentService.BuildEquippedSlots(container, itemsById);

        var slot = Assert.Single(slots);
        Assert.Equal(7, slot.SlotIndex);
        Assert.Equal(100, slot.Item.ItemId);
        Assert.Equal(5, slot.Enchant);
        Assert.Equal(3, slot.Combine);
    }

    [Fact]
    public void BuildEquippedSlots_ItemIdMissingFromCatalog_IsSkippedNotThrown()
    {
        var itemsById = EmptyFrozen<int, ItemDefinition>();
        var container = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(999999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var slots = EquipmentService.BuildEquippedSlots(container, itemsById);

        Assert.Empty(slots);
    }

    [Fact]
    public void RecomputeStats_EquippingAVitalityItem_RaisesMaxLife()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [200] = new(Item(200, 100), ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();
        var worldData = WorldData(itemsById);
        var attributes = NoBonusAttributes();

        var withoutEquipment = EquipmentService.RecomputeStats(attributes, ImmutableDictionary<byte, ItemStack>.Empty,
            worldData);

        var equipped = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(2, new ItemStack(200, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)); // slot 2 = armor
        var withEquipment = EquipmentService.RecomputeStats(attributes, equipped, worldData);

        Assert.True(withEquipment.MaxLife > withoutEquipment.MaxLife);
    }
}
