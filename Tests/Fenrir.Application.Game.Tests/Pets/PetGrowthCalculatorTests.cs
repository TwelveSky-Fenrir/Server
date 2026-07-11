using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetGrowthCalculatorTests
{
    private static FrozenDictionary<int, ItemDefinition> Items(params (int Id, byte Sort)[] items)
    {
        var dict = new Dictionary<int, ItemDefinition>();
        foreach (var (id, sort) in items)
            dict[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = sort }, []);
        return dict.ToFrozenDictionary();
    }

    [Fact]
    public void Compute_NoPetEquipped_ReturnsDefault()
    {
        var result = PetGrowthCalculator.Compute(0, 1000, 100, Items());

        Assert.Equal(default, result);
    }

    [Fact]
    public void Compute_ZeroGrowth_ReturnsDefault()
    {
        var result = PetGrowthCalculator.Compute(1004, 0, 100, Items((1004, 22)));

        Assert.Equal(default, result);
    }

    [Fact]
    public void Compute_NotAGrowablePetSort_ReturnsDefault()
    {
        var result = PetGrowthCalculator.Compute(1004, 1000, 100, Items((1004, 28)));

        Assert.Equal(default, result);
    }

    [Fact]
    public void Compute_LifeFamily0_LinearBelowMax_UsesK2000()
    {
        var items = Items((1004, 22));
        var result = PetGrowthCalculator.Compute(1004, 20_000_000, 100, items);

        Assert.Equal(1000, result.Life);
    }

    [Fact]
    public void Compute_LifeFamily0_AtOrAboveMax_ClampsToCap()
    {
        var items = Items((1004, 22));
        var result = PetGrowthCalculator.Compute(1004, 100_000_000, 100, items);

        Assert.Equal(2200, result.Life);
    }

    [Fact]
    public void Compute_LifePremiumId1310_UsesDoubleKAndCap()
    {
        var items = Items((1310, 22));
        var result = PetGrowthCalculator.Compute(1310, 320_000_000, 100, items);

        Assert.Equal(4400, result.Life);
    }

    [Fact]
    public void Compute_AttackPower_GatedByActivity_ZeroWhenInactive()
    {
        var items = Items((541, 22));

        var active = PetGrowthCalculator.Compute(541, 20_000_000, 1, items);
        var inactive = PetGrowthCalculator.Compute(541, 20_000_000, 0, items);

        Assert.True(active.AttackPower > 0);
        Assert.Equal(0, inactive.AttackPower);
    }

    [Fact]
    public void Compute_LifeAndDefense_NotGatedByActivity_VerifiedSourceNuance()
    {
        var items = Items((1004, 22), (542, 22));

        var lifeInactive = PetGrowthCalculator.Compute(1004, 20_000_000, 0, items);
        var defenseInactive = PetGrowthCalculator.Compute(542, 20_000_000, 0, items);

        Assert.True(lifeInactive.Life > 0);
        Assert.True(defenseInactive.DefensePower > 0);
    }

    [Fact]
    public void Compute_AttackPremiumId1312_UsesDoubleKAndCap()
    {
        var items = Items((1312, 22));
        var result = PetGrowthCalculator.Compute(1312, 320_000_000, 1, items);

        Assert.Equal(2200, result.AttackPower);
    }

    [Fact]
    public void Compute_DefensePremiumId1311_UsesQuadrupleKAndCap()
    {
        var items = Items((1311, 22));
        var result = PetGrowthCalculator.Compute(1311, 320_000_000, 1, items);

        Assert.Equal(4400, result.DefensePower);
    }
}
