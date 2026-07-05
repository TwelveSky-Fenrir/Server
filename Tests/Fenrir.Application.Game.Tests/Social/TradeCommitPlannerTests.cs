using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social.Trade;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeCommitPlannerTests
{
    private static ItemStack Stack(int itemId)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void BuildFinalContainers_RemovesOwnOfferedItem_AndAddsReceivedItemToFreeSlot()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, Stack(100)) // offered away
            .Add(1, Stack(200)); // kept

        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var ownOffered = new (byte, byte, ItemStack)?[]
        {
            (ContainerMatrix.InventoryPage0, 0, Stack(100))
        };
        var received = new (byte, byte, ItemStack)?[]
        {
            (ContainerMatrix.InventoryPage0, 5, Stack(999)) // source container/slot irrelevant on the receiving side
        };

        var plan = TradeCommitPlanner.BuildFinalContainers(page0, page1, ownOffered, received);

        Assert.False(plan.Overflowed);
        Assert.Equal(200, plan.Page0[1].ItemId); // untouched
        Assert.Equal(999, plan.Page0[0].ItemId); // slot 0 vacated by the offered item, then reused for the received one
    }

    [Fact]
    public void BuildFinalContainers_Page0Full_OverflowsIntoPage1()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        for (var i = 0; i <= 63; i++)
            builder.Add((byte)i, Stack(1));
        var page0 = builder.ToImmutable();
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var received = new (byte, byte, ItemStack)?[] { (ContainerMatrix.InventoryPage0, 0, Stack(999)) };

        var plan = TradeCommitPlanner.BuildFinalContainers(page0, page1, [], received);

        Assert.False(plan.Overflowed);
        Assert.Equal(999, plan.Page1[0].ItemId);
    }

    [Fact]
    public void BuildFinalContainers_BothPagesFull_Overflows()
    {
        var builder0 = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        var builder1 = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        for (var i = 0; i <= 63; i++)
        {
            builder0.Add((byte)i, Stack(1));
            builder1.Add((byte)i, Stack(1));
        }

        var received = new (byte, byte, ItemStack)?[] { (ContainerMatrix.InventoryPage0, 0, Stack(999)) };

        var plan = TradeCommitPlanner.BuildFinalContainers(builder0.ToImmutable(), builder1.ToImmutable(), [],
            received);

        Assert.True(plan.Overflowed);
    }

    [Fact]
    public void BuildFinalContainers_NullSlots_AreSkipped()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty;
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        (byte, byte, ItemStack)?[] ownOffered = [null, null];
        (byte, byte, ItemStack)?[] received = [null];

        var plan = TradeCommitPlanner.BuildFinalContainers(page0, page1, ownOffered, received);

        Assert.False(plan.Overflowed);
        Assert.Empty(plan.Page0);
        Assert.Empty(plan.Page1);
    }
}
