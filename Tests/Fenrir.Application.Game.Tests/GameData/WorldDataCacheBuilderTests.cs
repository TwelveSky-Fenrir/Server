using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;
using static Fenrir.Application.Game.Tests.GameData.WorldDataTestRows;

namespace Fenrir.Application.Game.Tests.GameData;

/// <summary>Index construction and orphan filtering of <see cref="WorldDataCacheBuilder" /> on in-memory rows.</summary>
public class WorldDataCacheBuilderTests
{
    [Fact]
    public void BuildZones_FiltersPortalsWithoutDestination_AndCountsThem()
    {
        var (zones, stats) = WorldDataCacheBuilder.BuildZones(
            [Zone(1), Zone(2)],
            [
                Portal(1, 0, 2),
                Portal(1, 1, null),
                Portal(1, 2, 2),
                Portal(2, 0, null)
            ],
            [],
            [],
            []);

        Assert.Equal(2, stats.PortalsWithoutDestination);
        Assert.Equal(new[] { 0, 2 }, zones[1].Portals.Select(p => (int)p.SlotIndex));
        Assert.Empty(zones[2].Portals);
    }

    [Fact]
    public void BuildZones_FiltersSpawnRegionsWithoutZoneOrMonster_AndGroupsByZone()
    {
        var (zones, stats) = WorldDataCacheBuilder.BuildZones(
            [Zone(1), Zone(2)],
            [],
            [],
            [],
            [
                SpawnRegion(10, 1, 100),
                SpawnRegion(11, null, 100),
                SpawnRegion(12, 1, null),
                SpawnRegion(13, 2, 200),
                SpawnRegion(14, null, null) // zone check runs first, so a null-zone+null-monster row counts only once
            ]);

        Assert.Equal(2, stats.SpawnRegionsWithoutZone);
        Assert.Equal(1, stats.SpawnRegionsWithoutMonster);
        Assert.Equal(new[] { 10 }, zones[1].MonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
        Assert.Equal(new[] { 13 }, zones[2].MonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
    }

    [Fact]
    public void BuildZones_FiltersNpcPlacementsWithoutNpc()
    {
        var (zones, stats) = WorldDataCacheBuilder.BuildZones(
            [Zone(1)],
            [],
            [],
            [
                NpcSpawn(1, 0, 500),
                NpcSpawn(1, 1, null),
                NpcSpawn(1, 2, 501)
            ],
            []);

        Assert.Equal(1, stats.NpcPlacementsWithoutNpc);
        Assert.Equal(new[] { 500, 501 }, zones[1].NpcSpawns.Select(s => s.NpcId!.Value));
    }

    [Fact]
    public void BuildZones_KeepsAllSpawnPoints_AndFindsLandingByFromZone()
    {
        // unlike portals/regions, a null FromZoneNumber isn't an orphan here -- the row is never discarded
        var (zones, stats) = WorldDataCacheBuilder.BuildZones(
            [Zone(1), Zone(2)],
            [],
            [
                SpawnPoint(1, 0, 2),
                SpawnPoint(1, 1, null),
                SpawnPoint(2, 0, 1)
            ],
            [],
            []);

        Assert.Equal(0, stats.TotalDiscarded);
        Assert.Equal(2, zones[1].SpawnPoints.Length);

        var landing = zones[1].FindSpawnPointFrom(2);
        Assert.NotNull(landing);
        Assert.Equal(0, landing.SlotIndex);

        Assert.Null(zones[1].FindSpawnPointFrom(99));
    }

    [Fact]
    public void BuildItems_GroupsBonusSkillsByItem()
    {
        var items = WorldDataCacheBuilder.BuildItems(
            [Item(1), Item(2)],
            [
                new ItemBonusSkillRowDto(1, 0, 900, 5),
                new ItemBonusSkillRowDto(1, 1, 901, 7),
                new ItemBonusSkillRowDto(2, 0, null, 3)
            ]);

        Assert.Equal(new[] { 900, 901 }, items[1].BonusSkills.Select(s => s.SkillId!.Value));
        Assert.Single(items[2].BonusSkills);
        Assert.Null(items[2].BonusSkills[0].SkillId);
    }

    [Fact]
    public void BuildSkills_GroupsDescriptionsAndGradesBySkill()
    {
        var skills = WorldDataCacheBuilder.BuildSkills(
            [Skill(1), Skill(2)],
            [
                new SkillDescriptionRowDto(1, 0, "line0"),
                new SkillDescriptionRowDto(1, 1, "line1")
            ],
            [
                SkillGrade(1, 0),
                SkillGrade(1, 1),
                SkillGrade(2, 0),
                SkillGrade(2, 1)
            ]);

        Assert.Equal(new[] { "line0", "line1" }, skills[1].Descriptions.Select(d => d.Text));
        Assert.Equal(new[] { 0, 1 }, skills[1].Grades.Select(g => (int)g.GradeIndex));
        Assert.Empty(skills[2].Descriptions);
        Assert.Equal(2, skills[2].Grades.Length);
    }

    [Fact]
    public void BuildMonsters_GroupsAllFiveDropTablesByMonsterId()
    {
        var monsters = WorldDataCacheBuilder.BuildMonsters(
            [Monster(1), Monster(2)],
            [new MonsterDropMoneyRowDto(1, 5000, 10, 20)],
            [
                new MonsterDropPotionRowDto(1, 0, 100, 7001),
                new MonsterDropPotionRowDto(1, 1, 200, 7002)
            ],
            [new MonsterDropExtraItemRowDto(1, 0, 50, 8001)],
            [
                new MonsterDropCategoryRateRowDto(1, 0, 10),
                new MonsterDropCategoryRateRowDto(2, 0, 20)
            ],
            [new MonsterDropQuestItemRowDto(1, 30, 9001)]);

        var withDrops = monsters[1];
        Assert.NotNull(withDrops.DropMoney);
        Assert.Equal(5000, withDrops.DropMoney.DropRate);
        Assert.Equal(new[] { 7001, 7002 }, withDrops.DropPotions.Select(p => p.PotionItemId));
        Assert.Single(withDrops.DropExtraItems);
        Assert.Single(withDrops.DropCategoryRates);
        Assert.NotNull(withDrops.DropQuestItem);
        Assert.Equal(9001, withDrops.DropQuestItem.QuestItemId);

        var bare = monsters[2];
        Assert.Null(bare.DropMoney);
        Assert.Empty(bare.DropPotions);
        Assert.Empty(bare.DropExtraItems);
        Assert.Single(bare.DropCategoryRates);
        Assert.Null(bare.DropQuestItem);
    }

    [Fact]
    public void BuildNpcs_GroupsAllFiveChildTablesByNpcId()
    {
        var npcs = WorldDataCacheBuilder.BuildNpcs(
            [Npc(1), Npc(2)],
            [
                new NpcMenuOptionRowDto(1, 0, 11),
                new NpcMenuOptionRowDto(1, 1, 12)
            ],
            [new NpcShopItemRowDto(1, 0, 0, 8001)],
            [new NpcSkillOfferRowDto(1, 1, 1, 0, null, null, 0, 900)],
            [new NpcSpeechRowDto(1, 0, 0, "hello")],
            [new NpcGambleCostRowDto(1, 0, 0, 1000)]);

        var merchant = npcs[1];
        Assert.Equal(new[] { 11, 12 }, merchant.MenuOptions.Select(m => m.OptionId));
        Assert.Single(merchant.ShopItems);
        Assert.Single(merchant.SkillOffers);
        Assert.Single(merchant.Speeches);
        Assert.Single(merchant.GambleCosts);

        var bare = npcs[2];
        Assert.Empty(bare.MenuOptions);
        Assert.Empty(bare.ShopItems);
        Assert.Empty(bare.SkillOffers);
        Assert.Empty(bare.Speeches);
        Assert.Empty(bare.GambleCosts);
    }

    [Fact]
    public void BuildQuests_GroupsRewardsAndSpeechesByQuest()
    {
        var quests = WorldDataCacheBuilder.BuildQuests(
            [Quest(1), Quest(2)],
            [
                new QuestRewardRowDto(1, 0, 6, 8001, null),
                new QuestRewardRowDto(1, 1, 2, null, 5000)
            ],
            [new QuestSpeechRowDto(1, 0, 0, "go", 0)]);

        Assert.Equal(2, quests[1].Rewards.Length);
        Assert.Single(quests[1].Speeches);
        Assert.Empty(quests[2].Rewards);
        Assert.Empty(quests[2].Speeches);
    }

    [Fact]
    public void BuildRewardBundles_KeepsBundleWithoutSlots()
    {
        var bundles = WorldDataCacheBuilder.BuildRewardBundles(
            [new RewardBundleRowDto(1), new RewardBundleRowDto(2)],
            [new RewardBundleItemRowDto(1, 1, 8001)]);

        Assert.Single(bundles[1]);
        Assert.Empty(bundles[2]);
    }

    [Theory]
    [InlineData("world.Items")]
    [InlineData("world.Monsters")]
    [InlineData("world.Zones")]
    [InlineData("world.Levels")]
    [InlineData("world.Skills")]
    public void Build_Throws_WhenACriticalDatasetIsEmpty(string dataset)
    {
        var rows = dataset switch
        {
            "world.Items" => MinimalRows() with { Items = [] },
            "world.Monsters" => MinimalRows() with { Monsters = [] },
            "world.Zones" => MinimalRows() with { Zones = [] },
            "world.Levels" => MinimalRows() with { Levels = [] },
            "world.Skills" => MinimalRows() with { Skills = [] },
            _ => throw new ArgumentOutOfRangeException(nameof(dataset))
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains(dataset, exception.Message);
    }

    [Fact]
    public void Build_PopulatesEveryIndex_FromMinimalRows()
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 2, 0, 0, 0, 0)],
            ItemMallProducts = [new ItemMallProductRowDto(1, 1, 8001, 1, 100, true)],
            BloodExchangeCatalog = [new BloodExchangeCatalogRowDto(0, 8001, 5, 1)],
            EventDefinitions = []
        };

        var (cache, stats) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.ItemsById);
        Assert.Single(cache.SkillsById);
        Assert.Single(cache.MonstersById);
        Assert.Single(cache.LevelsByLevel);
        Assert.Single(cache.ZonesByNumber);
        Assert.Single(cache.GemSocketsById);
        Assert.Single(cache.ItemMallProductsById);
        Assert.Single(cache.BloodExchangeCatalog);
        Assert.Empty(cache.EventDefinitions);
        Assert.Empty(cache.NpcsById);
        Assert.Empty(cache.QuestsById);
        Assert.Empty(cache.RewardBundleItemsByBundleId);
        Assert.Equal(0, stats.TotalDiscarded);
    }
}
