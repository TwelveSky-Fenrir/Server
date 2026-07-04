using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Pets;
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
        // item 1004 is a pet-family Life id, but here it is catalogued as sort 28 (Phoenix amulet), not 22.
        var result = PetGrowthCalculator.Compute(1004, 1000, 100, Items((1004, 28)));

        Assert.Equal(default, result);
    }

    [Fact]
    public void Compute_LifeFamily0_LinearBelowMax_UsesK2000()
    {
        // item 1004 -> Life family 0, max 40,000,000. Half-max growth -> half of the K=2000 cap.
        var items = Items((1004, 22));
        var result = PetGrowthCalculator.Compute(1004, 20_000_000, 100, items);

        Assert.Equal(1000, result.Life); // 20_000_000 * 2000 / 40_000_000
    }

    [Fact]
    public void Compute_LifeFamily0_AtOrAboveMax_ClampsToCap()
    {
        var items = Items((1004, 22));
        var result = PetGrowthCalculator.Compute(1004, 100_000_000, 100, items);

        Assert.Equal(2200, result.Life); // normal cap
    }

    [Fact]
    public void Compute_LifePremiumId1310_UsesDoubleKAndCap()
    {
        // 1310 is both a Life-family-3 member AND the verified Life premium id (K=4000, cap=4400).
        var items = Items((1310, 22));
        var result = PetGrowthCalculator.Compute(1310, 320_000_000, 100, items); // == family-3 max

        Assert.Equal(4400, result.Life);
    }

    [Fact]
    public void Compute_AttackPower_GatedByActivity_ZeroWhenInactive()
    {
        var items = Items((541, 22)); // Attack family 0

        var active = PetGrowthCalculator.Compute(541, 20_000_000, 1, items);
        var inactive = PetGrowthCalculator.Compute(541, 20_000_000, 0, items);

        Assert.True(active.AttackPower > 0);
        Assert.Equal(0, inactive.AttackPower);
    }

    [Fact]
    public void Compute_LifeAndDefense_NotGatedByActivity_VerifiedSourceNuance()
    {
        // Verified against GameSystem_07_Pet.cpp: ReturnLifeValue/ReturnDefensePower never reference their
        // own pActivityValue parameter at all -- only ReturnAttackPower does.
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

        Assert.Equal(2200, result.AttackPower); // premium cap, family 3 max = 320,000,000
    }

    [Fact]
    public void Compute_DefensePremiumId1311_UsesQuadrupleKAndCap()
    {
        var items = Items((1311, 22));
        var result = PetGrowthCalculator.Compute(1311, 320_000_000, 1, items);

        Assert.Equal(4400, result.DefensePower); // 1311's own premium cap (K=4000/cap4400)
    }
}
