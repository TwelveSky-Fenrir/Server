using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeItemPlacementResolverTests
{
    private static ItemDefinition StackableItem(int itemId, byte checkAvatarTrade = 0)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 2, CheckAvatarTrade = checkAvatarTrade },
            []);
    }

    private static ItemDefinition NonStackableItem(int itemId, byte checkAvatarTrade = 0)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 9, CheckAvatarTrade = checkAvatarTrade },
            []);
    }

    private static ItemStack Stack(int itemId, int quantity, byte enchant = 0, byte combine = 0, byte refine = 0,
        byte socket = 0, int gem1 = 0, int gem2 = 0, int gem3 = 0, int expireDate = 0, int serial = 0)
    {
        return new ItemStack(itemId, quantity, enchant, combine, refine, socket, gem1, gem2, gem3, expireDate,
            serial);
    }

    // ---- Shared gates (SourceEmpty / UnknownItem), exercised once via ResolveDeposit ----

    [Fact]
    public void ResolveDeposit_NoSourceItem_IsSourceEmpty()
    {
        var result = TradeItemPlacementResolver.ResolveDeposit(null, 1, null, StackableItem(1), true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.SourceEmpty, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ResolveDeposit_UnknownItemDefinition_IsUnknownItem()
    {
        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(1, 1), 1, null, null, true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.UnknownItem, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_CheckAvatarTradeFlagged_IsUntradeableItem()
    {
        var item = StackableItem(1, checkAvatarTrade: 1);

        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(1, 1), 1, null, item, true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.UntradeableItem, result.Outcome);
    }

    [Fact]
    public void ResolveWithdrawal_CheckAvatarTradeFlagged_IsNeverRechecked()
    {
        // Withdrawal must NOT re-check iCheckAvatarTrade -- only deposit gates on it.
        var item = StackableItem(1, checkAvatarTrade: 1);

        var result = TradeItemPlacementResolver.ResolveWithdrawal(Stack(1, 1), 1, null, item, true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ResolveRearrange_CheckAvatarTradeFlagged_IsNeverRechecked()
    {
        var item = NonStackableItem(1, checkAvatarTrade: 1);

        var result = TradeItemPlacementResolver.ResolveRearrange(Stack(1, 1), 0, null, item, true);

        Assert.True(result.Succeeded);
    }

    // ---- Stackable branch ----

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void ResolveDeposit_Stackable_QuantityOutOfRange_IsRejected(int requestedQuantity)
    {
        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(50, 999), requestedQuantity, null,
            StackableItem(50), true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.QuantityOutOfRange, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_Stackable_RequestExceedsSourceQuantity_IsInsufficientSourceQuantity()
    {
        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(50, 5), 6, null, StackableItem(50), true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.InsufficientSourceQuantity, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_Stackable_DifferentItemOccupyingDestination_IsRejected_NoSwap()
    {
        var destination = Stack(999, 1);

        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(50, 10), 10, destination, StackableItem(50),
            true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.DestinationIncompatible, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_Stackable_MergeWouldExceedCap_IsRejected()
    {
        var destination = Stack(50, 995);

        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(50, 10), 10, destination, StackableItem(50),
            true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.DestinationIncompatible, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_Stackable_FullMoveIntoEmptyDestination_ClearsSourceAndSetsSocketTripleWhenEligible()
    {
        var source = Stack(50, 10, enchant: 1, combine: 1, refine: 1, socket: 1, gem1: 7, gem2: 8, gem3: 9,
            expireDate: 20260101);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 10, null, StackableItem(50), true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource); // fully emptied source is cleared
        var destination = result.NewDestination!.Value;
        Assert.Equal(50, destination.ItemId);
        Assert.Equal(10, destination.Quantity);
        Assert.Equal(0, destination.Enchant);
        Assert.Equal(0, destination.Combine);
        Assert.Equal(0, destination.Refine);
        Assert.Equal(0, destination.Socket);
        Assert.Equal(0, destination.Serial);
        Assert.Equal(7, destination.SocketGem1);
        Assert.Equal(8, destination.SocketGem2);
        Assert.Equal(9, destination.SocketGem3);
        // Never assigned by SetInventoryQuantity: a fresh destination's ExpireDate is 0, not carried from source.
        Assert.Equal(0, destination.ExpireDate);
    }

    [Fact]
    public void ResolveDeposit_Stackable_FullMoveNotSocketEligible_ZeroesDestinationSocketTriple()
    {
        var source = Stack(50, 10, gem1: 7, gem2: 8, gem3: 9);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 10, null, StackableItem(50), false);

        var destination = result.NewDestination!.Value;
        Assert.Equal(0, destination.SocketGem1);
        Assert.Equal(0, destination.SocketGem2);
        Assert.Equal(0, destination.SocketGem3);
    }

    [Fact]
    public void ResolveDeposit_Stackable_PartialSplit_LeavesRemainderInSourceAndNeverTouchesDestinationSockets()
    {
        var source = Stack(50, 10, gem1: 7, gem2: 8, gem3: 9);
        var destination = Stack(50, 5, gem1: 111, gem2: 222, gem3: 333);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 4, destination, StackableItem(50), true);

        Assert.True(result.Succeeded);
        Assert.Equal(6, result.NewSource!.Value.Quantity); // 10 - 4 remains behind
        Assert.Equal(9, result.NewDestination!.Value.Quantity); // 5 + 4
        // Partial split never carries or clears socket-gem data at the destination.
        Assert.Equal(111, result.NewDestination.Value.SocketGem1);
        Assert.Equal(222, result.NewDestination.Value.SocketGem2);
        Assert.Equal(333, result.NewDestination.Value.SocketGem3);
    }

    [Fact]
    public void ResolveDeposit_Stackable_MergeOntoPopulatedSlot_AlwaysResetsValueAndSerialToZero()
    {
        var source = Stack(50, 10);
        var destination = Stack(50, 5, enchant: 9, combine: 9, refine: 9, socket: 9, serial: 12345);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 10, destination, StackableItem(50), true);

        var newDestination = result.NewDestination!.Value;
        Assert.Equal(0, newDestination.Enchant);
        Assert.Equal(0, newDestination.Combine);
        Assert.Equal(0, newDestination.Refine);
        Assert.Equal(0, newDestination.Socket);
        Assert.Equal(0, newDestination.Serial);
    }

    [Fact]
    public void ResolveWithdrawal_Stackable_NeverAssignsExpireDate_DestinationKeepsWhateverItHad()
    {
        var source = Stack(50, 10, expireDate: 20260101);
        var destination = Stack(50, 5, expireDate: 20261231);

        var result = TradeItemPlacementResolver.ResolveWithdrawal(source, 10, destination, StackableItem(50), true);

        Assert.Equal(20261231, result.NewDestination!.Value.ExpireDate);
    }

    // ---- Non-stackable branch ----

    [Fact]
    public void ResolveDeposit_NonStackable_DestinationOccupied_IsRejected_NoSwap()
    {
        var destination = Stack(700, 1);

        var result = TradeItemPlacementResolver.ResolveDeposit(Stack(700, 1), 0, destination, NonStackableItem(700),
            true);

        Assert.Equal(TradeItemPlacementResolver.PlacementOutcome.DestinationIncompatible, result.Outcome);
    }

    [Fact]
    public void ResolveDeposit_NonStackable_MovesWholeSlotAsOneUnit_AndCarriesExpireDateThrough()
    {
        var source = Stack(700, 1, enchant: 4, combine: 3, refine: 2, socket: 1, serial: 999, expireDate: 20260615);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 0, null, NonStackableItem(700), true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource); // source unconditionally cleared
        var destination = result.NewDestination!.Value;
        Assert.Equal(700, destination.ItemId);
        Assert.Equal(4, destination.Enchant);
        Assert.Equal(3, destination.Combine);
        Assert.Equal(2, destination.Refine);
        Assert.Equal(1, destination.Socket);
        Assert.Equal(999, destination.Serial);
        // Deliberate deviation from legacy data-loss: ExpireDate carries through instead of being stripped.
        Assert.Equal(20260615, destination.ExpireDate);
    }

    [Fact]
    public void ResolveDeposit_NonStackable_NotSocketEligible_ZeroesSocketGemTripleOnly()
    {
        var source = Stack(700, 1, gem1: 1, gem2: 2, gem3: 3, enchant: 5);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 0, null, NonStackableItem(700), false);

        var destination = result.NewDestination!.Value;
        Assert.Equal(0, destination.SocketGem1);
        Assert.Equal(0, destination.SocketGem2);
        Assert.Equal(0, destination.SocketGem3);
        Assert.Equal(5, destination.Enchant); // unrelated "value" byte still carries through
    }

    [Fact]
    public void ResolveDeposit_NonStackable_SocketEligible_CopiesSocketGemTriple()
    {
        var source = Stack(700, 1, gem1: 1, gem2: 2, gem3: 3);

        var result = TradeItemPlacementResolver.ResolveDeposit(source, 0, null, NonStackableItem(700), true);

        var destination = result.NewDestination!.Value;
        Assert.Equal(1, destination.SocketGem1);
        Assert.Equal(2, destination.SocketGem2);
        Assert.Equal(3, destination.SocketGem3);
    }

    [Fact]
    public void ResolveRearrange_NonStackable_TradeToTrade_BehavesLikeAnyOtherTransfer()
    {
        var source = Stack(700, 1, gem1: 4);

        var result = TradeItemPlacementResolver.ResolveRearrange(source, 0, null, NonStackableItem(700), true);

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.NewDestination!.Value.SocketGem1);
    }

    // ---- Bound-check helpers ----

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void IsValidTradeSlot_MatchesMaxTradeSlotNum(int slot, bool expected)
    {
        Assert.Equal(expected, TradeItemPlacementResolver.IsValidTradeSlot(slot));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    public void IsValidInventoryPage_MatchesMaxInventoryPageNum(int page, bool expected)
    {
        Assert.Equal(expected, TradeItemPlacementResolver.IsValidInventoryPage(page));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(63, true)]
    [InlineData(64, false)]
    [InlineData(-1, false)]
    public void IsValidInventorySlot_MatchesMaxInventorySlotNum(int slot, bool expected)
    {
        Assert.Equal(expected, TradeItemPlacementResolver.IsValidInventorySlot(slot));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(-1, false)]
    public void IsValidGridCoordinate_IsZeroToSeven(int coordinate, bool expected)
    {
        Assert.Equal(expected, TradeItemPlacementResolver.IsValidGridCoordinate(coordinate));
    }

    [Fact]
    public void IsPremiumPageAccessAllowed_ExpiredInThePast_IsRejected()
    {
        Assert.False(TradeItemPlacementResolver.IsPremiumPageAccessAllowed(100, 200));
    }

    [Fact]
    public void IsPremiumPageAccessAllowed_StillActive_IsAllowed()
    {
        Assert.True(TradeItemPlacementResolver.IsPremiumPageAccessAllowed(200, 100));
    }
}
