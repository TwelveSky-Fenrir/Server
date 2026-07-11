using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetSlotAmuletBonusTableTests
{
    private static FrozenDictionary<int, ItemDefinition> Items(params (int Id, byte Sort)[] items)
    {
        var dict = new Dictionary<int, ItemDefinition>();
        foreach (var (id, sort) in items)
            dict[id] = new ItemDefinition(WorldDataTestRows.Item(id) with { Sort = sort }, []);
        return dict.ToFrozenDictionary();
    }

    [Theory]
    [InlineData(76000, 3000f, 3000f)]
    [InlineData(76001, 3000f, 3000f)]
    [InlineData(76002, 3000f, 3000f)]
    [InlineData(76003, 3000f, 3000f)]
    [InlineData(76004, 3000f, 3000f)]
    [InlineData(76005, 5000f, 5000f)]
    [InlineData(76006, 7500f, 7500f)]
    [InlineData(76007, 12500f, 12500f)]
    public void GetBaseBonus_ConfirmedPhoenixFamilyIds_ReturnsConfirmedValues(int itemId, float expectedLife,
        float expectedMana)
    {
        var items = Items((itemId, PetSlotAmuletBonusTable.RequiredSortCode));

        var (life, mana) = PetSlotAmuletBonusTable.GetBaseBonus(itemId, items);

        Assert.Equal(expectedLife, life);
        Assert.Equal(expectedMana, mana);
    }

    [Fact]
    public void GetBaseBonus_Item8290_LifeAndManaDiffer()
    {
        var items = Items((8290, PetSlotAmuletBonusTable.RequiredSortCode));

        var (life, mana) = PetSlotAmuletBonusTable.GetBaseBonus(8290, items);

        Assert.Equal(550f, life);
        Assert.Equal(500f, mana);
    }

    [Fact]
    public void GetBaseBonus_WrongSortCode_ReturnsZeroEvenIfIdMatchesTable()
    {
        var items = Items((76005, 22));

        var (life, mana) = PetSlotAmuletBonusTable.GetBaseBonus(76005, items);

        Assert.Equal(0f, life);
        Assert.Equal(0f, mana);
    }

    [Fact]
    public void GetBaseBonus_ItemNotInReferenceTable_ReturnsZero()
    {
        var (life, mana) = PetSlotAmuletBonusTable.GetBaseBonus(76005, Items());

        Assert.Equal(0f, life);
        Assert.Equal(0f, mana);
    }

    [Fact]
    public void GetBaseBonus_QualifyingIdWithoutConfirmedMagnitude_ReturnsZero()
    {
        var items = Items((2151, PetSlotAmuletBonusTable.RequiredSortCode));

        Assert.Contains(2151, PetSlotAmuletBonusTable.QualifyingItemIds);

        var (life, mana) = PetSlotAmuletBonusTable.GetBaseBonus(2151, items);

        Assert.Equal(0f, life);
        Assert.Equal(0f, mana);
    }

    [Fact]
    public void QualifyingItemIds_ContainsExactly59Ids()
    {
        Assert.Equal(59, PetSlotAmuletBonusTable.QualifyingItemIds.Count);
    }

    [Theory]
    [InlineData(2151)]
    [InlineData(2154)]
    [InlineData(2174)]
    [InlineData(2189)]
    [InlineData(2195)]
    [InlineData(2206)]
    [InlineData(2253)]
    [InlineData(2254)]
    [InlineData(2261)]
    [InlineData(2262)]
    [InlineData(2301)]
    [InlineData(2302)]
    [InlineData(2410)]
    [InlineData(2421)]
    [InlineData(8290)]
    [InlineData(76000)]
    [InlineData(76007)]
    public void QualifyingItemIds_ContainsRangeBounds(int itemId)
    {
        Assert.Contains(itemId, PetSlotAmuletBonusTable.QualifyingItemIds);
    }

    [Theory]
    [InlineData(2150)]
    [InlineData(2155)]
    [InlineData(2422)]
    [InlineData(75999)]
    [InlineData(76008)]
    public void QualifyingItemIds_ExcludesJustOutsideRangeBounds(int itemId)
    {
        Assert.DoesNotContain(itemId, PetSlotAmuletBonusTable.QualifyingItemIds);
    }
}
