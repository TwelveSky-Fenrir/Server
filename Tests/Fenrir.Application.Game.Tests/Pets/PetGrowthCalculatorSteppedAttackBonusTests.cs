using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetGrowthCalculatorSteppedAttackBonusTests
{
    private static FrozenDictionary<int, ItemDefinition> Items(params (int Id, byte Sort)[] items)
    {
        var dict = new Dictionary<int, ItemDefinition>();
        foreach (var (id, sort) in items)
            dict[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = sort }, []);
        return dict.ToFrozenDictionary();
    }

    [Fact]
    public void Compute_ItemInBothTables_PopulatesSteppedAttackBonus()
    {
        var items = Items((1004, 22));

        var result = PetGrowthCalculator.Compute(1004, growth: 80_000_000, activity: 1, items);

        Assert.Equal(250, result.SteppedAttackBonus);
    }

    [Fact]
    public void Compute_ItemAbsentFromSteppedTable_SteppedAttackBonusIsZeroRegardlessOfGrowth()
    {
        var items = Items((541, 22));

        var result = PetGrowthCalculator.Compute(541, growth: 80_000_000, activity: 1, items);

        Assert.True(result.AttackPower > 0);
        Assert.Equal(0, result.SteppedAttackBonus);
    }

    [Fact]
    public void Compute_SteppedAttackBonus_GatedByActivityLikeItsOwnFormula()
    {
        var items = Items((1004, 22));

        var inactive = PetGrowthCalculator.Compute(1004, growth: 80_000_000, activity: 0, items);

        Assert.Equal(0, inactive.SteppedAttackBonus);
    }

    [Fact]
    public void Compute_SteppedAttackBonus_BelowThresholdGrowth_IsZero()
    {
        var items = Items((1004, 22));

        var result = PetGrowthCalculator.Compute(1004, growth: 1_000_000, activity: 1, items);

        Assert.Equal(0, result.SteppedAttackBonus);
    }
}
