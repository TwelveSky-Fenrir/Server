using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Pets;

/// <summary>
///     Covers the B8-pet-growth-depth wiring of <see cref="PetSteppedAttackPowerCategoryTable" /> into
///     <see cref="PetGrowthCalculator.Compute" />'s <c>SteppedAttackBonus</c> field -- previously always 0
///     in production because no Domain resolver populated it (see <c>StatCalculator.cs</c>'s own remarks).
///     Complements the existing family-table coverage in <c>PetGrowthCalculatorTests</c>, which predates
///     this field's wiring.
/// </summary>
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
        // 1004 is both a Life-family-0 member (PetGrowthCalculator) AND a category-0 member of
        // PetSteppedAttackPowerCategoryTable (tier max 40,000,000) -- fully evolved (200%+) and active.
        var items = Items((1004, 22));

        var result = PetGrowthCalculator.Compute(1004, growth: 80_000_000, activity: 1, items);

        Assert.Equal(250, result.SteppedAttackBonus);
    }

    [Fact]
    public void Compute_ItemAbsentFromSteppedTable_SteppedAttackBonusIsZeroRegardlessOfGrowth()
    {
        // 541 is a Life/Attack-family-0 member (PetGrowthCalculator's own tables) but is deliberately absent
        // from PetSteppedAttackPowerCategoryTable -- the contract's own verified table-membership gap.
        var items = Items((541, 22));

        var result = PetGrowthCalculator.Compute(541, growth: 80_000_000, activity: 1, items);

        Assert.True(result.AttackPower > 0); // the OTHER (flat, family-table) attack contribution still applies
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
