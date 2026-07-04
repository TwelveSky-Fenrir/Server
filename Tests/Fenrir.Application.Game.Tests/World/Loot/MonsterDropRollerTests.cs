using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class MonsterDropRollerTests
{
    /// <summary>world.Items must be non-empty for <see cref="WorldDataCacheBuilder.Build" />'s critical-dataset gate; these tests only exercise money/potion rolls.</summary>
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
        // Documented open issue: Fenrir has no Level2/rebirth-level field yet -- see IsEligible's own remarks.
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 10, MartialItemLevel = 1 };

        Assert.False(MonsterDropRoller.IsEligible(monster, 10));
    }

    [Fact]
    public void Roll_IneligibleMonster_DropsNothingAtAll()
    {
        var monster = WorldDataTestRows.Monster(1) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, new MonsterDropMoneyRowDto(1, 1_000_000, 100, 100));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(1));

        var result = roller.Roll(definition, 50, 0, 0);

        Assert.Null(result.Money);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Roll_MoneyDropRateAtMaximum_AlwaysDrops_WithLnw33Adjustment()
    {
        // DropRate at the RandomNumber() ceiling (1_000_000) always succeeds regardless of the RNG draw.
        var monster = WorldDataTestRows.Monster(2) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster,
            new MonsterDropMoneyRowDto(2, 1_000_000, 1000, 1000));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(42));

        var result = roller.Roll(definition, 1, 0, 0);

        // LNW33: size > 500 -> -30%, then +2000. 1000 -> 700 -> 2700.
        Assert.Equal(2700, result.Money);
    }

    [Fact]
    public void Roll_MoneyDropRateZero_NeverDrops()
    {
        var monster = WorldDataTestRows.Monster(3) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, new MonsterDropMoneyRowDto(3, 0, 100, 100));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(7));

        var result = roller.Roll(definition, 1, 0, 0);

        Assert.Null(result.Money);
    }

    [Fact]
    public void Roll_PotionDropRateAtMaximum_AlwaysDropsThatPotion()
    {
        var monster = WorldDataTestRows.Monster(4) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster, potions: (0, 1_000_000, 8001));
        var roller = new MonsterDropRoller(EmptyCache(), new Random(99));

        var result = roller.Roll(definition, 1, 0, 0);

        Assert.Contains(result.Items, item => item.ItemId == 8001 && item.Quantity == 1);
    }

    [Fact]
    public void Roll_NoMoneyRowAtAll_NeverCrashes_AndNeverDropsMoney()
    {
        var monster = WorldDataTestRows.Monster(5) with { ItemLevel = 1, MartialItemLevel = 0 };
        var definition = Definition(monster);
        var roller = new MonsterDropRoller(EmptyCache(), new Random(3));

        var result = roller.Roll(definition, 1, 0, 0);

        Assert.Null(result.Money);
    }
}
