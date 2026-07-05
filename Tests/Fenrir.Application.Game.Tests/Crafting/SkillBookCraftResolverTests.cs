using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Crafting;

public class SkillBookCraftResolverTests
{
    [Theory]
    [InlineData(SkillBookCraftCatalog.Recipe1Sort, 1054, 1055, 1056, 1057, 90567)]
    [InlineData(SkillBookCraftCatalog.Recipe2Sort, 1058, 1059, 1060, 1061, 90568)]
    [InlineData(SkillBookCraftCatalog.Recipe3Sort, 1062, 1063, 1064, 1065, 90569)]
    public void ResolveFragments_CorrectMaterials_ProducesExpectedBook(int sort, int material1, int material2,
        int material3, int material4, int expectedItemId)
    {
        var result = SkillBookCraftResolver.ResolveFragments(sort, material1, material2, material3, material4);

        Assert.True(result.Succeeded);
        Assert.Equal(expectedItemId, result.ResultItemId);
    }

    [Fact]
    public void ResolveFragments_WrongMaterialOrder_IsRejected()
    {
        var result = SkillBookCraftResolver.ResolveFragments(SkillBookCraftCatalog.Recipe1Sort, 1055, 1054, 1056,
            1057);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveFragments_SortOutOfRange_IsRejected()
    {
        var result = SkillBookCraftResolver.ResolveFragments(3, 1054, 1055, 1056, 1057);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveWarGod_WrongMaterial_IsRejected()
    {
        var result = SkillBookCraftResolver.ResolveWarGod(99301, 99301, 99303, 99304, 0,
            new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveWarGod_PreviousTribeThree_IsRejected()
    {
        // The legacy's own i8th[3][3] lookup table has no row for tribe 3 -- an out-of-bounds read in the
        // original, rejected here instead (D8: fix genuine UB, don't reproduce a crash).
        var result = SkillBookCraftResolver.ResolveWarGod(99301, 99302, 99303, 99304, 3,
            new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 0, 90787)]
    [InlineData(0, 2, 90786)]
    [InlineData(0, 1, 90788)]
    [InlineData(1, 0, 90789)]
    [InlineData(1, 2, 90790)]
    [InlineData(1, 1, 90791)]
    [InlineData(2, 0, 90793)]
    [InlineData(2, 2, 90792)]
    [InlineData(2, 1, 90794)]
    public void ResolveWarGod_PerTribeColumnSelection_MatchesLegacyTable(int previousTribe, int r1Mod10,
        int expectedSkillId)
    {
        var result = SkillBookCraftResolver.ResolveWarGod(99301, 99302, 99303, 99304, (byte)previousTribe,
            new ScriptedRandomSource(r1Mod10));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedSkillId, result.ResultItemId);
    }
}
