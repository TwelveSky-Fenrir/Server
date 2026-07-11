using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Crafting;

public class CraftResolverTests
{
    private static ItemStack Stack(int itemId, int quantity)
    {
        return new ItemStack(itemId, quantity, 3, 2, 1, 4, 0, 0, 0, 0, 777);
    }

    [Fact]
    public void Jade_BothSlotsPurpleJade_AlwaysSucceeds_ProducesRedJade()
    {
        var result = CraftResolver.ResolveJadeUpgrade(
            Stack(CraftRecipeCatalog.PurpleJadeItemId, 1),
            Stack(CraftRecipeCatalog.PurpleJadeItemId, 1));

        Assert.True(result.Succeeded);
        Assert.Equal(CraftRecipeCatalog.RedJadeItemId, result.ResultStack!.Value.ItemId);
    }

    [Fact]
    public void Jade_ResultResetsUpgradeBytesAndQuantity_ButKeepsSerial()
    {
        var m1 = Stack(CraftRecipeCatalog.PurpleJadeItemId, 1);
        var result = CraftResolver.ResolveJadeUpgrade(m1, Stack(CraftRecipeCatalog.PurpleJadeItemId, 1));

        Assert.Equal(0, result.ResultStack!.Value.Enchant);
        Assert.Equal(0, result.ResultStack.Value.Combine);
        Assert.Equal(0, result.ResultStack.Value.Refine);
        Assert.Equal(0, result.ResultStack.Value.Socket);
        Assert.Equal(0, result.ResultStack.Value.Quantity);
        Assert.Equal(m1.Serial, result.ResultStack.Value.Serial);
    }

    [Fact]
    public void Jade_WrongMaterial_IsRejected()
    {
        var result = CraftResolver.ResolveJadeUpgrade(
            Stack(CraftRecipeCatalog.PurpleJadeItemId, 1),
            Stack(999, 1));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(5, 5)]
    public void Jade_EitherSlotQuantityAboveOne_IsRejected(int quantity1, int quantity2)
    {
        var result = CraftResolver.ResolveJadeUpgrade(
            Stack(CraftRecipeCatalog.PurpleJadeItemId, quantity1),
            Stack(CraftRecipeCatalog.PurpleJadeItemId, quantity2));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(506)]
    [InlineData(507)]
    [InlineData(508)]
    [InlineData(578)]
    [InlineData(579)]
    [InlineData(509)]
    public void Elixir_AnyOfTheSixBaseElixirs_IsAccepted(int baseElixirId)
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(baseElixirId, 10), true,
            new ScriptedRandomSource(0));

        Assert.NotEqual(CraftResolver.ElixirOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Elixir_WrongMaterial_IsRejected()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(1019, 10), true, new ScriptedRandomSource(0));

        Assert.Equal(CraftResolver.ElixirOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Elixir_InsufficientQuantity_IsRejected()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 9), true, new ScriptedRandomSource(0));

        Assert.Equal(CraftResolver.ElixirOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Elixir_NoFreeInventorySlot_IsRejected()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 10), false,
            new ScriptedRandomSource(0));

        Assert.Equal(CraftResolver.ElixirOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Elixir_SuccessRoll_ProducesItemInRange801To806()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 10), true, new ScriptedRandomSource(0, 3));

        Assert.Equal(CraftResolver.ElixirOutcome.Success, result.Outcome);
        Assert.InRange(result.ResultItemId!.Value, 801, 806);
        Assert.Equal(804, result.ResultItemId.Value);
    }

    [Fact]
    public void Elixir_FailureRoll_StillConsumesMaterial_NoItemCreated()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 10), true, new ScriptedRandomSource(99));

        Assert.Equal(CraftResolver.ElixirOutcome.Failed, result.Outcome);
        Assert.Null(result.ResultItemId);
        Assert.Null(result.RemainingMaterial);
    }

    [Fact]
    public void Elixir_QuantityAboveExactTen_LeavesRemainder()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 25), true, new ScriptedRandomSource(0, 0));

        Assert.Equal(15, result.RemainingMaterial!.Value.Quantity);
    }

    [Fact]
    public void Elixir_QuantityExactlyTen_ClearsSlotEntirely()
    {
        var result = CraftResolver.ResolveAdvancedElixir(Stack(506, 10), true, new ScriptedRandomSource(99));

        Assert.Null(result.RemainingMaterial);
    }
}
