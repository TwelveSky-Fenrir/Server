using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World.Loot;

namespace Fenrir.Application.Game.Tests.Crafting;

public class PetCraftResolverTests
{
    private static ItemStack Stack(int itemId)
    {
        return new ItemStack(itemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 777);
    }

    private static ItemStack GrowthStack(int itemId, int growthValue)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(growthValue);
        return new ItemStack(itemId, 0, enchant, combine, refine, socket, 0, 0, 0, 0, 777);
    }

    [Theory]
    [InlineData(0, 1006)]
    [InlineData(7, 1006)]
    [InlineData(8, 1007)]
    [InlineData(39, 1010)]
    [InlineData(47, 1011)]
    public void Recipe1_SuccessRoll_ProducesWeightedPoolItem(int roll, int expectedItemId)
    {
        var result = PetCraftResolver.ResolveRecipe1(Stack(1002), Stack(1003), Stack(1004), Stack(1178),
            new ScriptedRandomSource(roll));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedItemId, result.ResultItemId);
        Assert.Equal(0, result.ResultQuantity);
        Assert.Equal(0, ItemValueCodec.Encode(result.Enchant, result.Combine, result.Refine, result.Socket));
    }

    [Theory]
    [InlineData(48)]
    [InlineData(99)]
    public void Recipe1_MissRoll_ProducesConsolationStackOfThree(int roll)
    {
        var result = PetCraftResolver.ResolveRecipe1(Stack(1002), Stack(1003), Stack(1004), Stack(1178),
            new ScriptedRandomSource(roll));

        Assert.True(result.Succeeded);
        Assert.Equal(PetCraftRecipeCatalog.ConsolationItemId, result.ResultItemId);
        Assert.Equal(3, result.ResultQuantity);
    }

    [Fact]
    public void Recipe1_WrongCatalyst_IsRejected()
    {
        var result = PetCraftResolver.ResolveRecipe1(Stack(1002), Stack(1003), Stack(1004), Stack(1179),
            new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Recipe1_WrongFusionMaterial_IsRejected()
    {
        var result = PetCraftResolver.ResolveRecipe1(Stack(999), Stack(1003), Stack(1004), Stack(1178),
            new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 1016)]
    [InlineData(2, 1012)]
    [InlineData(5, 1013)]
    [InlineData(8, 1014)]
    [InlineData(11, 1015)]
    public void Recipe2_SuccessRoll_ProducesWeightedPoolItem(int roll, int expectedItemId)
    {
        var result = PetCraftResolver.ResolveRecipe2(Stack(1006), Stack(1007), Stack(1008), Stack(1179),
            new ScriptedRandomSource(roll));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedItemId, result.ResultItemId);
        Assert.Equal(0, result.ResultQuantity);
    }

    [Fact]
    public void Recipe2_MissRoll_ProducesConsolationStackOfFifteen()
    {
        var result = PetCraftResolver.ResolveRecipe2(Stack(1006), Stack(1007), Stack(1008), Stack(1179),
            new ScriptedRandomSource(12));

        Assert.True(result.Succeeded);
        Assert.Equal(PetCraftRecipeCatalog.ConsolationItemId, result.ResultItemId);
        Assert.Equal(15, result.ResultQuantity);
    }

    [Fact]
    public void Recipe2_WrongCatalyst_IsRejected()
    {
        var result = PetCraftResolver.ResolveRecipe2(Stack(1006), Stack(1007), Stack(1008), Stack(1178),
            new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    // The C++ source rolls rand_mir()%100 here too, but immediately discards it (tValue[0] is unconditionally
    // overwritten to 1012 right after) -- verifying the outcome never varies regardless of roll is the point.
    [Fact]
    public void Recipe3_ValidMaterials_AlwaysProduces1012()
    {
        var result = PetCraftResolver.ResolveRecipe3(Stack(1013), Stack(1014), Stack(1015), Stack(1235));

        Assert.True(result.Succeeded);
        Assert.Equal(1012, result.ResultItemId);
    }

    [Fact]
    public void Recipe3_WrongMaterialOrder_IsRejected()
    {
        var result = PetCraftResolver.ResolveRecipe3(Stack(1014), Stack(1013), Stack(1015), Stack(1235));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Recipe4_TwoIronCondors_Produces1016()
    {
        var result = PetCraftResolver.ResolveRecipe4(Stack(1012), Stack(1012));

        Assert.True(result.Succeeded);
        Assert.Equal(1016, result.ResultItemId);
    }

    [Fact]
    public void Recipe4_MismatchedMaterials_IsRejected()
    {
        var result = PetCraftResolver.ResolveRecipe4(Stack(1012), Stack(1016));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 1310)]
    [InlineData(1, 1311)]
    [InlineData(2, 1312)]
    public void Recipe5_SufficientGrowth_ProducesPoolItemWithFreshGrowthValue(int roll, int expectedItemId)
    {
        var m1 = GrowthStack(1012, PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold);
        var m2 = GrowthStack(1016, PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold);

        var result = PetCraftResolver.ResolveRecipe5(m1, m2, new ScriptedRandomSource(roll));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedItemId, result.ResultItemId);
        Assert.Equal(PetCraftRecipeCatalog.GodRecipeResultGrowthValue,
            ItemValueCodec.Encode(result.Enchant, result.Combine, result.Refine, result.Socket));
    }

    [Fact]
    public void Recipe5_Material1GrowthBelowThreshold_IsRejected()
    {
        var m1 = GrowthStack(1012, PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold - 1);
        var m2 = GrowthStack(1016, PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold);

        var result = PetCraftResolver.ResolveRecipe5(m1, m2, new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Recipe5_Material2GrowthBelowThreshold_IsRejected()
    {
        var m1 = GrowthStack(1012, PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold);
        var m2 = GrowthStack(1016, PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold - 1);

        var result = PetCraftResolver.ResolveRecipe5(m1, m2, new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 2133)]
    [InlineData(1, 2144)]
    public void Recipe6_SufficientGrowth_ProducesPoolItem(int roll, int expectedItemId)
    {
        var m1 = GrowthStack(1012, PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold);
        var m2 = GrowthStack(2160, PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold);

        var result = PetCraftResolver.ResolveRecipe6(m1, m2, new ScriptedRandomSource(roll));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedItemId, result.ResultItemId);
    }

    [Fact]
    public void Recipe6_InsufficientGrowth_IsRejected()
    {
        var m1 = GrowthStack(1012, PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold);
        var m2 = GrowthStack(2160, PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold - 1);

        var result = PetCraftResolver.ResolveRecipe6(m1, m2, new ScriptedRandomSource(0));

        Assert.False(result.Succeeded);
    }
}
