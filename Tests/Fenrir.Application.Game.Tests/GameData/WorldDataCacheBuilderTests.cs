using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;
using static Fenrir.Application.Game.Tests.GameData.WorldDataTestRows;

namespace Fenrir.Application.Game.Tests.GameData;

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
                SpawnRegion(14, null, null)
            ]);

        Assert.Equal(2, stats.SpawnRegionsWithoutZone);
        Assert.Equal(1, stats.SpawnRegionsWithoutMonster);
        Assert.Equal(new[] { 10 }, zones[1].MonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
        Assert.Equal(new[] { 13 }, zones[2].MonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
    }

    [Fact]
    public void BuildZones_SeparatesBossSpawnRegionsFromFieldSpawnRegions_ByFileName()
    {
        var (zones, _) = WorldDataCacheBuilder.BuildZones(
            [Zone(1)],
            [],
            [],
            [],
            [
                SpawnRegion(20, 1, 100),
                BossSpawnRegion(21, 1, 500)
            ]);

        Assert.Equal(new[] { 20 }, zones[1].MonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
        Assert.Equal(new[] { 21 }, zones[1].BossMonsterSpawnRegions.Select(r => r.MonsterSpawnRegionId));
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
    [InlineData((short)0)]
    [InlineData((short)1001)]
    public void Build_Throws_WhenAQuestStepIsOutOfRange(short step)
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1) with { Step = step }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Quests", exception.Message);
        Assert.Contains("QuestId=1", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_000_001)]
    public void Build_Throws_WhenAQuestRewardAmountIsOutOfRange(int amount)
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1)],
            QuestRewards = [new QuestRewardRowDto(1, 0, 2, null, amount)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.QuestRewards", exception.Message);
        Assert.Contains("QuestId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenQuestSpeechExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1)],
            QuestSpeeches = [new QuestSpeechRowDto(1, 0, 0, new string('S', 51), 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.QuestSpeeches", exception.Message);
        Assert.Contains("QuestId=1", exception.Message);
    }

    [Fact]
    public void Build_Accepts_QuestSpeechAtMaxLength()
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1)],
            QuestSpeeches = [new QuestSpeechRowDto(1, 0, 0, new string('S', 50), 0)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.QuestsById[1].Speeches);
    }

    [Fact]
    public void Build_Accepts_QuestRewardWithNullAmount()
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1)],
            QuestRewards = [new QuestRewardRowDto(1, 0, 6, 8001, null)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.QuestsById[1].Rewards);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)1000)]
    public void Build_Accepts_QuestStepAtExactBounds(short step)
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1) with { Step = step }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(step, cache.QuestsById[1].Quest.Step);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000_000)]
    public void Build_Accepts_QuestRewardAmountAtExactBounds(int amount)
    {
        var rows = MinimalRows() with
        {
            Quests = [Quest(1)],
            QuestRewards = [new QuestRewardRowDto(1, 0, 2, null, amount)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(amount, cache.QuestsById[1].Rewards[0].Amount);
    }

    [Fact]
    public void Build_Throws_WhenLevelExpRangeMinExceedsMaxBound()
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, 2_000_000_001, 2_000_000_002, 0, 0, 0, 0, 0, 0, 0, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("ExpRangeMin", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenLevelExpRangeMaxExceedsMaxBound()
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, 0, 2_000_000_001, 0, 0, 0, 0, 0, 0, 0, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("ExpRangeMax", exception.Message);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 50)]
    public void Build_Throws_WhenLevelExpRangeMaxIsNotStrictlyGreaterThanExpRangeMin(int expRangeMin, int expRangeMax)
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, expRangeMin, expRangeMax, 0, 0, 0, 0, 0, 0, 0, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("invalid experience range", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenConsecutiveLevelsDoNotTileExactly()
    {
        var rows = MinimalRows() with
        {
            Levels =
            [
                new LevelRowDto(1, 0, 100, 0, 0, 0, 0, 0, 0, 100, 100),
                new LevelRowDto(2, 102, 200, 0, 0, 0, 0, 0, 0, 200, 200)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("must tile exactly", exception.Message);
    }

    [Fact]
    public void Build_Accepts_ConsecutiveLevelsThatTileExactly()
    {
        var rows = MinimalRows() with
        {
            Levels =
            [
                new LevelRowDto(1, 0, 100, 0, 0, 0, 0, 0, 0, 100, 100),
                new LevelRowDto(2, 101, 200, 0, 0, 0, 0, 0, 0, 200, 200)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(2, cache.LevelsByLevel.Count);
        Assert.Equal(101, cache.LevelsByLevel[2].ExpRangeMin);
    }

    [Fact]
    public void Build_Throws_WhenLevelRangeInfo3ExceedsMaxBound()
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, 0, 100, 101, 0, 0, 0, 0, 0, 0, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("RangeInfo3", exception.Message);
    }

    [Fact]
    public void Build_Accepts_LevelRangeInfo3AtExactUpperBound()
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, 0, 100, 100, 0, 0, 0, 0, 0, 0, 0)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal((byte)100, cache.LevelsByLevel[1].RangeInfo3);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10001, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 10001)]
    public void Build_Throws_WhenLevelLifeOrManaIsOutOfCombatStatBound(int life, int mana)
    {
        var rows = MinimalRows() with
        {
            Levels = [new LevelRowDto(1, 0, 100, 0, 0, 0, 0, 0, 0, life, mana)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Levels", exception.Message);
        Assert.Contains("combat stat, Life, or Mana", exception.Message);
    }

    [Fact]
    public void Build_Accepts_LevelRowAtExactUpperBounds()
    {
        var rows = MinimalRows() with
        {
            Levels =
            [
                new LevelRowDto(1, 0, 2_000_000_000, 100, 10000, 10000, 10000, 10000, 10000, 10000, 10000)
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        var level = cache.LevelsByLevel[1];
        Assert.Equal(2_000_000_000, level.ExpRangeMax);
        Assert.Equal((byte)100, level.RangeInfo3);
        Assert.Equal(10000, level.Life);
        Assert.Equal(10000, level.Mana);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)366)]
    public void Build_Throws_WhenItemCheckDateItemIsOutOfRange(short checkDateItem)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { CheckDateItem = checkDateItem }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Items", exception.Message);
        Assert.Contains("ItemId=1", exception.Message);
        Assert.Contains("CheckDateItem", exception.Message);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)365)]
    public void Build_Accepts_ItemCheckDateItemAtExactBounds(short checkDateItem)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { CheckDateItem = checkDateItem }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(checkDateItem, cache.ItemsById[1].Item.CheckDateItem);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)10001)]
    public void Build_Throws_WhenItemDataNumber3DIsOutOfRange(short dataNumber3D)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { DataNumber3D = dataNumber3D }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Items", exception.Message);
        Assert.Contains("DataNumber3D", exception.Message);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)10001)]
    public void Build_Throws_WhenItemAddDataNumber3DIsOutOfRange(short addDataNumber3D)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { AddDataNumber3D = addDataNumber3D }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Items", exception.Message);
        Assert.Contains("AddDataNumber3D", exception.Message);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)10000)]
    public void Build_Accepts_ItemDataNumber3DAndAddDataNumber3DAtExactBounds(short value)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { DataNumber3D = value, AddDataNumber3D = value }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(value, cache.ItemsById[1].Item.DataNumber3D);
        Assert.Equal(value, cache.ItemsById[1].Item.AddDataNumber3D);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)4)]
    public void Build_Throws_WhenPotionType2IsOutOfRestrictedRangeForPotionType1Nine(short potionType2)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { PotionType1 = 9, PotionType2 = potionType2 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Items", exception.Message);
        Assert.Contains("PotionType2", exception.Message);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)3)]
    public void Build_Accepts_PotionType2AtExactRestrictedBoundsForPotionType1Nine(short potionType2)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { PotionType1 = 9, PotionType2 = potionType2 }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(potionType2, cache.ItemsById[1].Item.PotionType2);
    }

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)10001)]
    public void Build_Throws_WhenPotionType2IsOutOfDefaultRangeForNonRestrictedPotionType1(short potionType2)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { PotionType1 = 0, PotionType2 = potionType2 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Items", exception.Message);
        Assert.Contains("PotionType2", exception.Message);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)10000)]
    public void Build_Accepts_PotionType2AtExactDefaultBoundsForNonRestrictedPotionType1(short potionType2)
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1) with { PotionType1 = 0, PotionType2 = potionType2 }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(potionType2, cache.ItemsById[1].Item.PotionType2);
    }

    [Fact]
    public void Build_Accepts_ItemIdZero_SkippingAllPerRowValidation()
    {
        var rows = MinimalRows() with
        {
            Items = [Item(1), Item(0) with { CheckDateItem = 500, DataNumber3D = 20000 }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(500, cache.ItemsById[0].Item.CheckDateItem);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)16)]
    public void Build_Throws_WhenAMonsterTypeIsOutOfRange(byte type)
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { Type = type }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
        Assert.Contains("Type=", exception.Message);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)15)]
    public void Build_Accepts_MonsterTypeAtExactBounds(byte type)
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { Type = type }]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(type, cache.MonstersById[1].Monster.Type);
    }

    [Fact]
    public void Build_Throws_WhenMonsterNameExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { Name = new string('X', 25) }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("Name", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2_000_000_001)]
    public void Build_Throws_WhenMonsterLifeIsOutOfRange(int life)
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { Life = life }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("Life=", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterRadiusInfo2IsLessThanRadiusInfo1()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { RadiusInfo1 = 100, RadiusInfo2 = 50 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("RadiusInfo1/RadiusInfo2", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterFollowInfo2IsLessThanFollowInfo1()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { FollowInfo1 = 50, FollowInfo2 = 10 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("FollowInfo1/FollowInfo2", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterSummonTime2IsLessThanSummonTime1()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { SummonTime1 = 100, SummonTime2 = 50 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("SummonTime1/SummonTime2", exception.Message);
    }

    [Fact]
    public void Build_Accepts_MonsterRowAtExactUpperBounds()
    {
        var rows = MinimalRows() with
        {
            Monsters =
            [
                Monster(1) with
                {
                    Name = new string('X', 24),
                    ChatLine1 = new string('Y', 100),
                    ChatLine2 = new string('Z', 100),
                    Type = 15,
                    SpecialType = 53,
                    DamageType = 2,
                    DataSortNumber = 10000,
                    Size1 = 1000,
                    Size2 = 1000,
                    Size3 = 1000,
                    Size4 = 1000,
                    SizeCategory = 4,
                    CheckCollision = 2,
                    TotalHitNum = 3,
                    TotalSkillHitNum = 3,
                    ItemLevel = 145,
                    MartialItemLevel = 25,
                    RealLevel = 1000,
                    MartialRealLevel = 1000,
                    GeneralExperience = 100_000_000,
                    PatExperience = 100_000_000,
                    Life = 2_000_000_000,
                    AttackType = 6,
                    RadiusInfo1 = 10000,
                    RadiusInfo2 = 10000,
                    WalkSpeed = 1000,
                    RunSpeed = 1000,
                    DeathSpeed = 1000,
                    AttackPower = 1_000_000,
                    DefensePower = 100_000,
                    AttackSuccess = 100_000,
                    AttackBlock = 100_000,
                    ElementAttackPower = 100_000,
                    ElementDefensePower = 100_000,
                    Critical = 100,
                    FollowInfo1 = 100,
                    FollowInfo2 = 100,
                    SummonTime1 = 1_000_000,
                    SummonTime2 = 1_000_000,
                    FrameInfo1 = 10000,
                    FrameInfo2 = 10000,
                    FrameInfo3 = 10000,
                    FrameInfo4 = 10000,
                    FrameInfo5 = 10000,
                    FrameInfo6 = 10000,
                    HitFrame1 = 10000,
                    HitFrame2 = 10000,
                    HitFrame3 = 10000,
                    SkillHitFrame1 = 10000,
                    SkillHitFrame2 = 10000,
                    SkillHitFrame3 = 10000,
                    BulletInfo1 = 10000,
                    BulletInfo2 = 10000
                }
            ]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Equal(2_000_000_000, cache.MonstersById[1].Monster.Life);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropMoneyDropRateIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropMoney = [new MonsterDropMoneyRowDto(1, 1_000_001, 0, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropMoney", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropPotionsPotionItemIdIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropPotions = [new MonsterDropPotionRowDto(1, 0, 0, 100_000)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropPotions", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropCategoryRatesValueIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropCategoryRates = [new MonsterDropCategoryRateRowDto(1, 0, 1_000_001)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropCategoryRates", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropExtraItemsItemIdIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropExtraItems = [new MonsterDropExtraItemRowDto(1, 0, 0, 100_000)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropExtraItems", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Accepts_MonsterDropExtraItemWithNullItemId()
    {
        var rows = MinimalRows() with
        {
            MonsterDropExtraItems = [new MonsterDropExtraItemRowDto(1, 0, 0, null)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Null(cache.MonstersById[1].DropExtraItems[0].ItemId);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropQuestItemsQuestItemIdIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropQuestItems = [new MonsterDropQuestItemRowDto(1, 0, 100_000)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropQuestItems", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterChatLineExceedsMaxLength()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { ChatLine1 = new string('C', 101) }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
        Assert.Contains("ChatLine", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterAnimationFrameInfoIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            Monsters = [Monster(1) with { FrameInfo1 = 10001 }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.Monsters", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
        Assert.Contains("FrameInfo1-6", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropMoneyMinAmountIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropMoney = [new MonsterDropMoneyRowDto(1, 0, 100_000_001, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropMoney", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropMoneyMaxAmountIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropMoney = [new MonsterDropMoneyRowDto(1, 0, 0, 100_000_001)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropMoney", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropPotionsDropRateIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropPotions = [new MonsterDropPotionRowDto(1, 0, 1_000_001, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropPotions", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropExtraItemsDropRateIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropExtraItems = [new MonsterDropExtraItemRowDto(1, 0, 1_000_001, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropExtraItems", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenMonsterDropQuestItemsDropRateIsOutOfRange()
    {
        var rows = MinimalRows() with
        {
            MonsterDropQuestItems = [new MonsterDropQuestItemRowDto(1, 1_000_001, 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.MonsterDropQuestItems", exception.Message);
        Assert.Contains("MonsterId=1", exception.Message);
    }

    [Theory]
    [InlineData(99, 0, 999_999, 999_999)]
    [InlineData(0, 999, 999_999, 999_999)]
    public void Build_Accepts_GemSocketZeroBypassCombinations(int type, int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, type, 0, value02, value03, value04)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(47)]
    public void Build_Throws_WhenGemSocketTypeIsOutsideTheLegacy1To46Range(int type)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, type, 0, 5, 5, 5)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.GemSockets", exception.Message);
        Assert.Contains("GemSocketId=1", exception.Message);
    }

    [Theory]
    [InlineData(-1, 100, 100)]
    [InlineData(34, 100, 100)]
    [InlineData(1, 401, 100)]
    [InlineData(1, 100, 401)]
    public void Build_Throws_WhenGemSocketType1BandIsViolated(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 1, 0, value02, value03, value04)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.GemSockets", exception.Message);
        Assert.Contains("GemSocketId=1", exception.Message);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(33, 400, 400)]
    public void Build_Accepts_GemSocketType1BandAtExactBounds(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 1, 0, value02, value03, value04)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Theory]
    [InlineData(-1, 500, 500)]
    [InlineData(101, 500, 500)]
    [InlineData(50, 1001, 500)]
    [InlineData(50, 500, 1001)]
    public void Build_Throws_WhenGemSocketMidBandTypeIsViolated(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 15, 0, value02, value03, value04)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.GemSockets", exception.Message);
        Assert.Contains("GemSocketId=1", exception.Message);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(100, 1000, 1000)]
    public void Build_Accepts_GemSocketMidBandTypeAtExactBounds(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 15, 0, value02, value03, value04)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Fact]
    public void Build_Accepts_GemSocketDeadBandType30To38Unconstrained()
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 34, 0, 999_999, 999_999, 999_999)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Theory]
    [InlineData(-1, 5, 0)]
    [InlineData(11, 5, 0)]
    [InlineData(5, 0, 0)]
    [InlineData(5, 5, 1)]
    public void Build_Throws_WhenGemSocketLowReqBandTypeIsViolated(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 40, 0, value02, value03, value04)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.GemSockets", exception.Message);
        Assert.Contains("GemSocketId=1", exception.Message);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(10, 1_000_000, 0)]
    public void Build_Accepts_GemSocketLowReqBandTypeAtExactBounds(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 40, 0, value02, value03, value04)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Theory]
    [InlineData(-1, 10, 0)]
    [InlineData(11, 10, 0)]
    [InlineData(5, 5, 0)]
    [InlineData(5, 10, 1)]
    public void Build_Throws_WhenGemSocketHighReqBandTypeIsViolated(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 44, 0, value02, value03, value04)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => WorldDataCacheBuilder.Build(rows));

        Assert.Contains("world.GemSockets", exception.Message);
        Assert.Contains("GemSocketId=1", exception.Message);
    }

    [Theory]
    [InlineData(1, 6, 0)]
    [InlineData(10, 1_000_000, 0)]
    public void Build_Accepts_GemSocketHighReqBandTypeAtExactBounds(int value02, int value03, int value04)
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(1, 44, 0, value02, value03, value04)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        Assert.Single(cache.GemSocketsById);
    }

    [Fact]
    public void Build_PopulatesGemSocketsById_WithAllFieldValues_KeyedByGemSocketId()
    {
        var rows = MinimalRows() with
        {
            GemSockets = [new GemSocketRowDto(7, 40, 3, 5, 10, 0)]
        };

        var (cache, _) = WorldDataCacheBuilder.Build(rows);

        var gemSocket = Assert.Single(cache.GemSocketsById.Values);
        Assert.Equal(7, gemSocket.GemSocketId);
        Assert.Equal(40, gemSocket.Type);
        Assert.Equal(3, gemSocket.Value01);
        Assert.Equal(5, gemSocket.Value02);
        Assert.Equal(10, gemSocket.Value03);
        Assert.Equal(0, gemSocket.Value04);
        Assert.Same(gemSocket, cache.GemSocketsById[7]);
    }

    [Theory]
    [InlineData("world.Items")]
    [InlineData("world.Monsters")]
    [InlineData("world.Zones")]
    [InlineData("world.ZonePortals")]
    [InlineData("world.Levels")]
    [InlineData("world.Skills")]
    public void Build_Throws_WhenACriticalDatasetIsEmpty(string dataset)
    {
        var rows = dataset switch
        {
            "world.Items" => MinimalRows() with { Items = [] },
            "world.Monsters" => MinimalRows() with { Monsters = [] },
            "world.Zones" => MinimalRows() with { Zones = [] },
            "world.ZonePortals" => MinimalRows() with { ZonePortals = [] },
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
