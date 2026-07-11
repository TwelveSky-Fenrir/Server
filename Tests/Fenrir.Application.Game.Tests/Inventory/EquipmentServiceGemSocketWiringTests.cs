using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Workstream B13-socket-prerequisites -- the full pipeline from a persisted <see cref="ItemStack" />'s
///     packed <c>SOCKET_GEM_V2</c> columns (<see cref="ItemStack.SocketGem1" />/<see cref="ItemStack.SocketGem2" />/
///     <see cref="ItemStack.SocketGem3" />, already DB-backed) through <see cref="EquipmentService.BuildEquippedSlots" />
///     and <see cref="EquipmentService.RecomputeStats" /> into a real gem-socket AttackPower contribution, using
///     <see cref="WorldDataCache.GemSocketsByTypeAndValue" /> as the effect table. The routing/decode math itself
///     is pinned by <c>GemSocketContributionTests</c> (Stats project); this file only pins that
///     <see cref="EquipmentService" /> actually threads the packed columns and the table through end to end.
/// </summary>
public class EquipmentServiceGemSocketWiringTests
{
    private static ItemRowDto Item(int itemId)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, 0, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }

    private static WorldDataCache WorldData(
        FrozenDictionary<int, ItemDefinition> itemsById,
        FrozenDictionary<int, GemSocketRowDto>? gemSocketsByTypeAndValue = null)
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
            GemSocketsByTypeAndValue = gemSocketsByTypeAndValue ?? EmptyFrozen<int, GemSocketRowDto>(),
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
        return new CharacterBaseAttributes(0, 100, 0, 0, 1, 0, 0, 0, 0, 0);
    }

    // Packs SOCKET_GEM_V2 as 3 little-endian ints: [0] unused, [1] count, then up to 5 (type,value) byte pairs.
    private static (int P1, int P2, int P3) Pack(byte count, params (byte Type, byte Value)[] pairs)
    {
        Span<byte> bytes = stackalloc byte[12];
        bytes[1] = count;
        for (var i = 0; i < pairs.Length && i < 5; i++)
        {
            bytes[2 + i * 2] = pairs[i].Type;
            bytes[3 + i * 2] = pairs[i].Value;
        }

        return (
            BinaryPrimitives.ReadInt32LittleEndian(bytes[..4]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4)));
    }

    [Fact]
    public void BuildEquippedSlots_CarriesTheItemStacksPackedSocketGemColumns()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [100] = new(Item(100), ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();
        var (p1, p2, p3) = Pack(1, (2, 50));

        var container = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(100, 1, 0, 0, 0, 0, p1, p2, p3, 0, 0));

        var slots = EquipmentService.BuildEquippedSlots(container, itemsById);

        var slot = Assert.Single(slots);
        Assert.Equal(p1, slot.SocketGem1);
        Assert.Equal(p2, slot.SocketGem2);
        Assert.Equal(p3, slot.SocketGem3);
    }

    [Fact]
    public void RecomputeStats_SocketedGem_RaisesAttackPower_WhenEffectTableHasAMatch()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [100] = new(Item(100), ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();
        var (p1, p2, p3) = Pack(1, (2, 50));
        var effectTable = new Dictionary<int, GemSocketRowDto>
        {
            [StatCalculator.GemSocketTypeValueKey(2, 50)] = new GemSocketRowDto(1, 2, 0, 50, 77, 0)
        }.ToFrozenDictionary();
        var worldData = WorldData(itemsById, effectTable);
        var attributes = NoBonusAttributes();

        var unsocketed = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var socketed = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(100, 1, 0, 0, 0, 0, p1, p2, p3, 0, 0));

        var withoutGem = EquipmentService.RecomputeStats(attributes, unsocketed, worldData);
        var withGem = EquipmentService.RecomputeStats(attributes, socketed, worldData);

        Assert.Equal(withoutGem.AttackPower + 77, withGem.AttackPower);
    }

    [Fact]
    public void RecomputeStats_SocketedGem_NoEffectTableRowMatch_LeavesAttackPowerUnchanged()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [100] = new(Item(100), ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();
        var (p1, p2, p3) = Pack(1, (2, 50));
        // WorldData's GemSocketsByTypeAndValue defaults to empty -- no (2,50) row exists anywhere.
        var worldData = WorldData(itemsById);
        var attributes = NoBonusAttributes();

        var unsocketed = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(100, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var socketed = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(100, 1, 0, 0, 0, 0, p1, p2, p3, 0, 0));

        var withoutGem = EquipmentService.RecomputeStats(attributes, unsocketed, worldData);
        var withGem = EquipmentService.RecomputeStats(attributes, socketed, worldData);

        Assert.Equal(withoutGem.AttackPower, withGem.AttackPower);
    }
}
