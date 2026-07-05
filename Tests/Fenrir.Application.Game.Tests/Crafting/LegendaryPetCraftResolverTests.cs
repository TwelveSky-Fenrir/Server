using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Crafting;

public class LegendaryPetCraftResolverTests
{
    private const byte LegendaryPool1Sort = 31;
    private const byte LegendaryPool2Sort = 32;

    [Fact]
    public void Material1SortNotLegendary_IsRejected()
    {
        var result = LegendaryPetCraftResolver.Resolve(30, 1878, 2150, 10000, new ScriptedRandomSource(5));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(1, 2150)]
    [InlineData(1878, 1)]
    public void WrongCatalystItemId_IsRejected(int material2ItemId, int material3ItemId)
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool1Sort, material2ItemId, material3ItemId, 10000,
            new ScriptedRandomSource(5));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void InsufficientContributionPoints_IsRejected()
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool1Sort, 1878, 2150, 9999,
            new ScriptedRandomSource(5));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ExactContributionPointThreshold_Succeeds()
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool1Sort, 1878, 2150,
            LegendaryPetCraftCatalog.ContributionPointCost, new ScriptedRandomSource(9, 0));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void TierRollZero_ProducesGuardianPoolItem()
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool1Sort, 1878, 2150, 10000,
            new ScriptedRandomSource(0, 3));

        Assert.True(result.Succeeded);
        Assert.Equal(LegendaryPetCraftCatalog.GuardianPool[3], result.ResultItemId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TierRollOneOrTwo_ProducesLegendaryPool2Item(int tierRoll)
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool2Sort, 1878, 2150, 10000,
            new ScriptedRandomSource(tierRoll, 2));

        Assert.True(result.Succeeded);
        Assert.Equal(LegendaryPetCraftCatalog.LegendaryPool2[2], result.ResultItemId);
    }

    [Fact]
    public void TierRollThreeOrAbove_ProducesLegendaryPool1Item()
    {
        var result = LegendaryPetCraftResolver.Resolve(LegendaryPool1Sort, 1878, 2150, 10000,
            new ScriptedRandomSource(9, 4));

        Assert.True(result.Succeeded);
        Assert.Equal(LegendaryPetCraftCatalog.LegendaryPool1[4], result.ResultItemId);
    }
}
