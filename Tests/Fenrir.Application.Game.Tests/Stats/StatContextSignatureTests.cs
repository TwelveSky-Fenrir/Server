using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Stats;

public class StatContextSignatureTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    private static CosmeticContext PopulatedCosmetic()
    {
        return new CosmeticContext(CostumeState: 1);
    }

    private static ZoneContext PopulatedZone()
    {
        return new ZoneContext(
            241,
            true,
            100,
            50,
            RageGauge: 999,
            GuildBuffActive: true,
            GuildId: 42);
    }

    private static ConsumableContext PopulatedConsumable()
    {
        return new ConsumableContext(
            HpBoostActive: true,
            WarriorPillActive: true,
            MaxPotionEventNum: 20,
            EventTribe: 1);
    }

    private static MountContext PopulatedMount()
    {
        return new MountContext(
            AnimalGrade: 4,
            RuntimeAttributes: [10, 20, 30]);
    }

    private static CharacterBaseAttributes RichAttributes()
    {
        return new CharacterBaseAttributes(
            120, 90, 75, 60,
            100, 0, 0, 305, 40, 8);
    }

    private static FrozenDictionary<short, LevelRowDto> RichLevels()
    {
        return new Dictionary<short, LevelRowDto>
        {
            [100] = new(100, 0, 100, 0, 250, 300, 40, 35, 20, 500, 400)
        }.ToFrozenDictionary();
    }

    private static ItemRowDto Item(
        int itemId, byte sort = 0, byte checkSetItem = 0,
        short strength = 0, short dexterity = 0, short vitality = 0, short intelligent = 0, short luck = 0,
        short attackPower = 0, short defensePower = 0, short attackSuccess = 0, short attackBlock = 0,
        short elementAttackPower = 0, short elementDefensePower = 0, byte critical = 0, byte capeInfo2 = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, checkSetItem, 0,
            strength, dexterity, vitality, intelligent, luck,
            attackPower, defensePower, attackSuccess, attackBlock,
            elementAttackPower, elementDefensePower, critical,
            0, 0, null,
            0, 0, 0, capeInfo2, 0);
    }

    private static EquippedItemSlot[] RichEquipment()
    {
        return
        [
            new EquippedItemSlot(2, Item(90002, 1, vitality: 30, defensePower: 40, attackBlock: 5), 12, 6, 0, 0),
            new EquippedItemSlot(3, Item(90003, checkSetItem: 2, attackSuccess: 20, luck: 4), 8, 4, 0, 0),
            new EquippedItemSlot(7, Item(90007, 14, strength: 25, attackPower: 60, critical: 3), 10, 5, 0, 0),
            new EquippedItemSlot(4, Item(90004, intelligent: 15, elementAttackPower: 12, elementDefensePower: 9), 6, 3,
                0, 0)
        ];
    }

    private static BuffInfo RichBuffs()
    {
        var buffs = new BuffInfo { Buff = new int[70] };
        buffs.Buff[0] = 25;
        buffs.Buff[2] = 10;
        buffs.Buff[8] = 15;
        return buffs;
    }

    [Fact]
    public void ComputeBaseStats_PopulatedContexts_ProduceIdenticalResultToDefaults()
    {
        var attributes = RichAttributes();
        var equipment = RichEquipment();
        var levels = RichLevels();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var baseline = StatCalculator.ComputeBaseStats(attributes, equipment, levels, 5, pet);

        var withContexts = StatCalculator.ComputeBaseStats(
            attributes, equipment, levels, 5, pet,
            PopulatedCosmetic(), PopulatedZone(),
            PopulatedConsumable(), PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void ComputeEffectiveStats_PopulatedContexts_ProduceIdenticalResultToDefaults()
    {
        var attributes = RichAttributes();
        var equipment = RichEquipment();
        var levels = RichLevels();
        var buffs = RichBuffs();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var baseline = StatCalculator.ComputeEffectiveStats(attributes, equipment, levels, buffs,
            5, pet);

        var withContexts = StatCalculator.ComputeEffectiveStats(
            attributes, equipment, levels, buffs, 5, pet,
            PopulatedCosmetic(), PopulatedZone(),
            PopulatedConsumable(), PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void ComputeEffectiveStats_NoEquipmentWithPopulatedContexts_StillMatchesDefaults()
    {
        var attributes = RichAttributes();
        var levels = RichLevels();

        var baseline = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels);

        var withContexts = StatCalculator.ComputeEffectiveStats(
            attributes, NoEquipment, levels,
            cosmetic: PopulatedCosmetic(), zone: PopulatedZone(),
            consumable: PopulatedConsumable(), mount: PopulatedMount());

        Assert.Equal(baseline, withContexts);
    }

    [Fact]
    public void Contexts_DefaultInstances_AreNeutral()
    {
        var cosmetic = default(CosmeticContext);
        var zone = default(ZoneContext);
        var consumable = default(ConsumableContext);
        var mount = default(MountContext);

        Assert.True(cosmetic.RuneItemIds.IsDefaultOrEmpty);
        Assert.True(cosmetic.RuneStatValues.IsDefaultOrEmpty);
        Assert.Equal(0, cosmetic.CostumeNumber);
        Assert.Equal(0, cosmetic.StellarCoreNumber);

        Assert.Equal(0, zone.ZoneNumber);
        Assert.False(zone.OrnamentInUse);
        Assert.Equal(0, zone.RankBuffType);
        Assert.Equal(0, zone.RageGauge);

        Assert.Equal(0, consumable.EatLifePotion);
        Assert.False(consumable.HpBoostActive);
        Assert.False(consumable.WarriorPillActive);

        Assert.Equal(0, mount.AnimalNumber);
        Assert.False(mount.AbsorbActive);
        Assert.True(mount.RuntimeAttributes.IsDefaultOrEmpty);
    }


    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }

    private static WorldDataCache WorldData()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [90002] = new(Item(90002, 1, vitality: 30, defensePower: 40, attackBlock: 5),
                ImmutableArray<ItemBonusSkillRowDto>.Empty),
            [90007] = new(Item(90007, 14, strength: 25, attackPower: 60, critical: 3),
                ImmutableArray<ItemBonusSkillRowDto>.Empty)
        }.ToFrozenDictionary();

        return new WorldDataCache
        {
            ItemsById = itemsById,
            SkillsById = EmptyFrozen<int, SkillDefinition>(),
            MonstersById = EmptyFrozen<int, MonsterDefinition>(),
            NpcsById = EmptyFrozen<int, NpcDefinition>(),
            QuestsById = EmptyFrozen<int, QuestDefinition>(),
            LevelsByLevel = RichLevels(),
            ZonesByNumber = EmptyFrozen<short, ZoneDefinition>(),
            GemSocketsById = EmptyFrozen<int, GemSocketRowDto>(),
            GemSocketsByTypeAndValue = EmptyFrozen<int, GemSocketRowDto>(),
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = EmptyFrozen<int, ItemMallProductRowDto>(),
            RewardBundleItemsByBundleId = EmptyFrozen<int, ImmutableArray<RewardBundleItemRowDto>>(),
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static ImmutableDictionary<byte, ItemStack> EquipmentContainer()
    {
        return ImmutableDictionary<byte, ItemStack>.Empty
            .Add(2, new ItemStack(90002, 1, 12, 6, 0, 0, 0, 0, 0, 0, 0))
            .Add(7, new ItemStack(90007, 1, 10, 5, 0, 0, 0, 0, 0, 0, 0));
    }

    private static PlayerRuntimeState PopulatedState()
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 100,
            MapId = 241,
            CostumeState = 1,
            UseOrnament = true,
            GuildId = 42,
            GuildBuffActive = true,
            AnimalNumber = 1234,
            AnimalAbsorbState = 1,
            MountRolledAttributes = [10, 20, 30]
        };
    }

    [Fact]
    public void RecomputeStats_WithPopulatedRuntimeState_MatchesNullRuntimeState()
    {
        var attributes = RichAttributes();
        var worldData = WorldData();
        var equipment = EquipmentContainer();
        var buffs = RichBuffs();
        var pet = new PetStatContribution(50, 40, 30, 20);

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData, buffs, pet);
        var withState = EquipmentService.RecomputeStats(attributes, equipment, worldData, buffs, pet, PopulatedState());

        Assert.Equal(withoutState, withState);
    }
}
