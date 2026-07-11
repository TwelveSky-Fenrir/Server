using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class MonsterDropRollerTests
{
    private static readonly Func<int, bool> NeverHasItem = _ => false;

        private static WorldDataCache EmptyCache()
    {
        return WorldDataCacheBuilder.Build(WorldDataTestRows.MinimalRows()).Cache;
    }

    private static MonsterDefinition Definition(MonsterRowDto monster,
        MonsterDropMoneyRowDto? money = null,
        params (int slot, int rate, int itemId)[] potions)
    {
        return new MonsterDefinition(
            monster,
            money,
            [.. potions.Select(p => new MonsterDropPotionRowDto(monster.MonsterId, (byte)p.slot, p.rate, p.itemId))],
            [],
            [],
            null);
    }

    private static MonsterDefinition DefinitionWithQuestItem(MonsterRowDto monster,
        MonsterDropQuestItemRowDto dropQuestItem)
    {
        return new MonsterDefinition(monster, null, [], [], [], dropQuestItem);
    }

    [Fact]
    public void IsEligible_LevelGapAtNine_StillEligible()
    {
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 10, MartialItemLevel = 0 };

        Assert.True(MonsterDropRoller.IsEligible(monster, 19));
    }

    [Fact]
    public void IsEligible_LevelGapOverNine_NotEligible()
    {
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 10, MartialItemLevel = 0 };

        Assert.False(MonsterDropRoller.IsEligible(monster, 20));
    }

    [Fact]
    public void IsEligible_MartialItemLevelMonsters_NeverEligible_InThisPass()
    {
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 10, MartialItemLevel = 1 };

        Assert.False(MonsterDropRoller.IsEligible(monster, 10));
    }

    [Fact]
    public void Roll_IneligibleMonster_DropsNothingAtAll()
    {
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, new MonsterDropMoneyRowDto(1, 1_000_000, 100, 100));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(1));

        var result = roller.Roll(definition, 50, 0, 0, QuestProgress.None, NeverHasItem);

        Assert.Null(result.Money);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Roll_MoneyDropRateAtMaximum_AlwaysDrops_WithLnw33Adjustment()
    {
        var monster = WorldDataTestRows.Monster(2) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster,
            new MonsterDropMoneyRowDto(2, 1_000_000, 1000, 1000));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(42));

        var result = roller.Roll(definition, 1, 0, 0, QuestProgress.None, NeverHasItem);

        Assert.Equal(2700, result.Money);
    }

    [Fact]
    public void Roll_MoneyDropRateZero_NeverDrops()
    {
        var monster = WorldDataTestRows.Monster(3) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, new MonsterDropMoneyRowDto(3, 0, 100, 100));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(7));

        var result = roller.Roll(definition, 1, 0, 0, QuestProgress.None, NeverHasItem);

        Assert.Null(result.Money);
    }

    [Fact]
    public void Roll_PotionDropRateAtMaximum_AlwaysDropsThatPotion()
    {
        var monster = WorldDataTestRows.Monster(4) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, potions: (0, 1_000_000, 8001));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(99));

        var result = roller.Roll(definition, 1, 0, 0, QuestProgress.None, NeverHasItem);

        Assert.Contains(result.Items, item => item.ItemId == 8001 && item.Quantity == 1);
    }

    [Fact]
    public void Roll_NoMoneyRowAtAll_NeverCrashes_AndNeverDropsMoney()
    {
        var monster = WorldDataTestRows.Monster(5) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster);
        var roller = new MonsterDropRoller(EmptyCache(), new Random(3));

        var result = roller.Roll(definition, 1, 0, 0, QuestProgress.None, NeverHasItem);

        Assert.Null(result.Money);
    }

    [Fact]
    public void Roll_QuestItemDropRateAtMaximum_MatchingActiveQuestWithoutItem_DropsQuestItem()
    {
        var monster = WorldDataTestRows.Monster(10) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(10, 1_000_000, 9001));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(1));
        var quest = new QuestProgress(5, 1, 2, 9001, 0);

        var result = roller.Roll(definition, 200, 0, 0, quest, NeverHasItem);

        Assert.Contains(result.Items, item => item.ItemId == 9001 && item.Quantity == 1);
        Assert.Null(result.Money);
    }

    [Fact]
    public void Roll_QuestItemAlreadyHeldByKiller_DoesNotDropAgain()
    {
        var monster = WorldDataTestRows.Monster(11) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(11, 1_000_000, 9002));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(2));
        var quest = new QuestProgress(0, 1, 2, 9002, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, itemId => itemId == 9002);

        Assert.DoesNotContain(result.Items, item => item.ItemId == 9002);
    }

    [Fact]
    public void Roll_QuestSortNotItemAcquisition_DoesNotDropQuestItem()
    {
        var monster = WorldDataTestRows.Monster(12) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(12, 1_000_000, 9003));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(3));
        var quest = new QuestProgress(0, 1, 1, 9003, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, NeverHasItem);

        Assert.DoesNotContain(result.Items, item => item.ItemId == 9003);
    }

    [Fact]
    public void Roll_QuestTargetPhaseMismatch_DoesNotDropQuestItem()
    {
        var monster = WorldDataTestRows.Monster(13) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(13, 1_000_000, 9004));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(4));
        var quest = new QuestProgress(0, 1, 2, 1234, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, NeverHasItem);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Roll_QuestItemDropRateZero_NeverDrops()
    {
        var monster = WorldDataTestRows.Monster(14) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(14, 0, 9005));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(5));
        var quest = new QuestProgress(0, 1, 2, 9005, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, NeverHasItem);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Roll_QuestItemNotActive_ActiveFlagZero_DoesNotDropEvenIfSortAndPhaseMatch()
    {
        var monster = WorldDataTestRows.Monster(15) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = DefinitionWithQuestItem(monster, new MonsterDropQuestItemRowDto(15, 1_000_000, 9006));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(6));
        var quest = new QuestProgress(0, 0, 2, 9006, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, NeverHasItem);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Roll_ItemsList_QuestItemPrecedesExtraItem_MatchingPipelineOrder()
    {
        var monster = WorldDataTestRows.Monster(16) with { ItemLevel = 1, MartialItemLevel = 0 };
        var extraItemRow = WorldDataTestRows.Item(2001) with { CheckMonsterDrop = 2 };
        var cache = WorldDataCacheBuilder.Build(WorldDataTestRows.MinimalRows() with { Items = [extraItemRow] })
            .Cache;
        var definition = new MonsterDefinition(
            monster,
            null,
            [],
            [new MonsterDropExtraItemRowDto(16, 0, 1_000_000, 2001)],
            [],
            new MonsterDropQuestItemRowDto(16, 1_000_000, 9007));
        var roller = new MonsterDropRoller(cache, new Random(7));
        var quest = new QuestProgress(0, 1, 2, 9007, 0);

        var result = roller.Roll(definition, 1, 0, 0, quest, NeverHasItem);

        var items = result.Items.Select(item => item.ItemId).ToList();
        Assert.True(items.IndexOf(9007) < items.IndexOf(2001));
    }
}
