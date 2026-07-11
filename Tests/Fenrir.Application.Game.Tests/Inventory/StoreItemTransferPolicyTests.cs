using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class StoreItemTransferPolicyTests
{
    private static ItemStack Stack(int itemId, int quantity = 1, byte enchant = 0, byte combine = 0,
        byte refine = 0, byte socket = 0, int gem1 = 0, int gem2 = 0, int gem3 = 0, int expireDate = 0,
        int serial = 0)
    {
        return new ItemStack(itemId, quantity, enchant, combine, refine, socket, gem1, gem2, gem3, expireDate,
            serial);
    }


    [Theory]
    [InlineData(0, ContainerMatrix.StorePage0)]
    [InlineData(1, ContainerMatrix.StorePage1)]
    public void TryResolveStoreContainer_ValidRawPage_Resolves(int rawPage, byte expected)
    {
        var ok = StoreItemTransferPolicy.TryResolveStoreContainer(rawPage, out var container);

        Assert.True(ok);
        Assert.Equal(expected, container);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void TryResolveStoreContainer_InvalidRawPage_Fails(int rawPage)
    {
        Assert.False(StoreItemTransferPolicy.TryResolveStoreContainer(rawPage, out _));
    }

    [Theory]
    [InlineData(0, ContainerMatrix.InventoryPage0)]
    [InlineData(1, ContainerMatrix.InventoryPage1)]
    public void TryResolveInventoryContainer_ValidRawPage_Resolves(int rawPage, byte expected)
    {
        var ok = StoreItemTransferPolicy.TryResolveInventoryContainer(rawPage, out var container);

        Assert.True(ok);
        Assert.Equal(expected, container);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void TryResolveInventoryContainer_InvalidRawPage_Fails(int rawPage)
    {
        Assert.False(StoreItemTransferPolicy.TryResolveInventoryContainer(rawPage, out _));
    }


    [Fact]
    public void Deposit_SourceOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 64, 0, ContainerMatrix.StorePage0, 0,
            Stack(1), null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Deposit_DestinationOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 28,
            Stack(1), null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Fact]
    public void Deposit_SecondInventoryPageNotAccessible_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage1, 0, 0, ContainerMatrix.StorePage0, 0,
            Stack(1), null, false, false, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Deposit_SecondStorePageNotAccessible_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage1, 0,
            Stack(1), null, false, false, true, false);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Deposit_BothFirstPages_IgnoresBothGates()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 0,
            Stack(1), null, false, false, false, false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Deposit_EmptySource_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 0,
            null, null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Fact]
    public void Deposit_NonStackable_MovesWholeSlotAndIgnoresQuantity()
    {
        var source = Stack(100, 1, 5, 1, 2, 3, 7, 8, 9,
            20260101, 42);

        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 999, ContainerMatrix.StorePage0, 0,
            source, null, false, true, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        Assert.True(result.IsNonStackableTransfer);
        var dest = result.NewDestination!.Value;
        Assert.Equal(100, dest.ItemId);
        Assert.Equal(5, dest.Enchant);
        Assert.Equal(1, dest.Combine);
        Assert.Equal(2, dest.Refine);
        Assert.Equal(3, dest.Socket);
        Assert.Equal(42, dest.Serial);
        Assert.Equal(7, dest.SocketGem1);
        Assert.Equal(8, dest.SocketGem2);
        Assert.Equal(9, dest.SocketGem3);
        Assert.Equal(20260101, dest.ExpireDate);
    }

    [Fact]
    public void Deposit_NonStackable_SocketUnsupported_ZeroesGemsButKeepsExpiry()
    {
        var source = Stack(100, gem1: 7, gem2: 8, gem3: 9, expireDate: 20260101);

        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 0,
            source, null, false, false, true, true);

        var dest = result.NewDestination!.Value;
        Assert.Equal(0, dest.SocketGem1);
        Assert.Equal(0, dest.SocketGem2);
        Assert.Equal(0, dest.SocketGem3);
        Assert.Equal(20260101, dest.ExpireDate);
    }

    [Fact]
    public void Deposit_NonStackable_DestinationOccupied_IsConflict()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 0,
            Stack(100), Stack(200), false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Deposit_Stackable_PartialIntoEmptyDestination_SplitsAndZeroesDestinationValueAndSerial()
    {
        var source = Stack(2, 10, 9, serial: 5);

        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 4, ContainerMatrix.StorePage0, 0,
            source, null, true, true, true, true);

        Assert.True(result.Succeeded);
        Assert.False(result.IsNonStackableTransfer);
        Assert.Equal(6, result.NewSource!.Value.Quantity);
        var dest = result.NewDestination!.Value;
        Assert.Equal(4, dest.Quantity);
        Assert.Equal(0, dest.Enchant);
        Assert.Equal(0, dest.Serial);
    }

    [Fact]
    public void Deposit_Stackable_FullTransferEmptiesSource_CopiesGemAndExpiryThenClearsSource()
    {
        var source = Stack(2, 4, gem1: 1, gem2: 2, gem3: 3, expireDate: 20260601);

        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 4, ContainerMatrix.StorePage0, 0,
            source, null, true, true, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        var dest = result.NewDestination!.Value;
        Assert.Equal(1, dest.SocketGem1);
        Assert.Equal(2, dest.SocketGem2);
        Assert.Equal(3, dest.SocketGem3);
        Assert.Equal(20260601, dest.ExpireDate);
    }

    [Fact]
    public void Deposit_Stackable_MergeIntoExistingDestination_LeavesBothSlotsSocketAndExpiryUntouchedWhenPartial()
    {
        var source = Stack(2, 10, gem1: 1, expireDate: 111);
        var destination = Stack(2, 5, gem1: 99, expireDate: 222);

        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 3, ContainerMatrix.StorePage0, 0,
            source, destination, true, true, true, true);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.NewSource!.Value.Quantity);
        Assert.Equal(1, result.NewSource!.Value.SocketGem1);
        var dest = result.NewDestination!.Value;
        Assert.Equal(8, dest.Quantity);
        Assert.Equal(99, dest.SocketGem1);
        Assert.Equal(222, dest.ExpireDate);
    }

    [Fact]
    public void Deposit_Stackable_QuantityExceedsSource_IsInvalid()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 5, ContainerMatrix.StorePage0, 0,
            Stack(2, 3), null, true, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Deposit_Stackable_QuantityAboveCap_IsInvalid()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 1000, ContainerMatrix.StorePage0, 0,
            Stack(2, 1000), null, true, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Deposit_Stackable_NonPositiveQuantity_IsInvalid()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0, ContainerMatrix.StorePage0, 0,
            Stack(2, 10), null, true, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Deposit_Stackable_DestinationDifferentItem_IsConflict()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 1, ContainerMatrix.StorePage0, 0,
            Stack(2, 10), Stack(3), true, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Deposit_Stackable_MergeWouldExceedCap_IsConflict()
    {
        var result = StoreItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 500, ContainerMatrix.StorePage0, 0,
            Stack(2, 500), Stack(2, 999 - 100), true, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }


    [Fact]
    public void Withdraw_SourceOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 28, 0, ContainerMatrix.InventoryPage0, 0, 0, 0,
            Stack(1), null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
    }

    [Fact]
    public void Withdraw_DestinationOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.InventoryPage0, 64, 0, 0,
            Stack(1), null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 8)]
    public void Withdraw_DestinationCoordinatesOutOfRange_Fails(int xPost, int yPost)
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.InventoryPage0, 0, xPost, yPost,
            Stack(1), null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationCoordinateOutOfRange, result.Outcome);
    }

    [Fact]
    public void Withdraw_SecondStorePageNotAccessible_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage1, 0, 0, ContainerMatrix.InventoryPage0, 0, 0, 0,
            Stack(1), null, false, false, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Withdraw_SecondInventoryPageNotAccessible_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.InventoryPage1, 0, 0, 0,
            Stack(1), null, false, false, true, false);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Withdraw_EmptySource_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, null, false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Fact]
    public void Withdraw_Stackable_MergesIntoExistingInventoryStack()
    {
        var source = Stack(2, 10);
        var destination = Stack(2, 3);

        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 4, ContainerMatrix.InventoryPage0, 0, 5, 6,
            source, destination, true, false, true, true);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.NewDestination!.Value.Quantity);
        Assert.Equal(6, result.NewSource!.Value.Quantity);
    }

    [Fact]
    public void Withdraw_NonStackable_DestinationOccupied_IsConflict()
    {
        var result = StoreItemTransferPolicy.ResolveWithdrawToInventory(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.InventoryPage0, 0, 0, 0,
            Stack(100), Stack(200), false, false, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }


    [Fact]
    public void Rearrange_SameSlot_IsNoOp()
    {
        var source = Stack(1, 5);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 3, 0, ContainerMatrix.StorePage0, 3,
            source, source, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.NoOp, result.Outcome);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rearrange_DifferentPageSameSlotIndex_IsNotNoOp()
    {
        var source = Stack(1, 5);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 3, 0, ContainerMatrix.StorePage1, 3,
            source, null, false, true);

        Assert.NotEqual(StoreItemTransferPolicy.TransferOutcome.NoOp, result.Outcome);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rearrange_SourceOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 28, 0, ContainerMatrix.StorePage0, 0,
            Stack(1), null, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
    }

    [Fact]
    public void Rearrange_DestinationOutOfRange_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage0, 28,
            Stack(1), null, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Fact]
    public void Rearrange_SourceOnSecondPage_GateAppliesEvenWhenDestinationIsFirstPage()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage1, 0, 0, ContainerMatrix.StorePage0, 1,
            Stack(1), null, false, false);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Rearrange_DestinationOnSecondPage_GateAppliesEvenWhenSourceIsFirstPage()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage1, 1,
            Stack(1), null, false, false);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SecondPageExpired, result.Outcome);
    }

    [Fact]
    public void Rearrange_NeitherSideOnSecondPage_IgnoresGate()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage0, 1,
            Stack(1), null, false, false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rearrange_EmptySource_Fails()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage0, 1,
            null, null, false, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Fact]
    public void Rearrange_NonStackable_DestinationOccupied_SwapsInstead()
    {
        var source = Stack(100, 1, 4);
        var destination = Stack(200, 1, 9);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage0, 1,
            source, destination, false, true);

        Assert.True(result.Succeeded);
        Assert.False(result.IsNonStackableTransfer);
        Assert.Equal(destination, result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void Rearrange_NonStackable_EmptyDestination_MovesWholeSlot()
    {
        var source = Stack(100, 1, 4, gem1: 1, expireDate: 55, serial: 9);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 999, ContainerMatrix.StorePage0, 1,
            source, null, false, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void Rearrange_Stackable_ZeroQuantity_MovesEntireStack()
    {
        var source = Stack(2, 20);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 0, ContainerMatrix.StorePage0, 1,
            source, null, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        Assert.Equal(20, result.NewDestination!.Value.Quantity);
    }

    [Fact]
    public void Rearrange_Stackable_PartialMoveLeavesRemainderInSource()
    {
        var source = Stack(2, 20);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 5, ContainerMatrix.StorePage0, 1,
            source, null, true, true);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewSource!.Value.Quantity);
        Assert.Equal(5, result.NewDestination!.Value.Quantity);
    }

    [Fact]
    public void Rearrange_Stackable_NegativeQuantity_IsInvalid()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, -1, ContainerMatrix.StorePage0, 1,
            Stack(2, 20), null, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Rearrange_Stackable_QuantityAboveCap_IsInvalid()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 1000, ContainerMatrix.StorePage0, 1,
            Stack(2, 1000), null, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Rearrange_Stackable_MismatchedItem_FullStackMove_SwapsWholeRecords()
    {
        var source = Stack(2, 20, 3, gem1: 1, expireDate: 111);
        var destination = Stack(3, 5, 7, gem1: 2, expireDate: 222);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 20, ContainerMatrix.StorePage0, 1,
            source, destination, true, true);

        Assert.True(result.Succeeded);
        Assert.Equal(destination, result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void Rearrange_Stackable_MismatchedItem_PartialQuantity_IsRejectedNotSwapped()
    {
        var source = Stack(2, 20);
        var destination = Stack(3, 5);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 10, ContainerMatrix.StorePage0, 1,
            source, destination, true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Rearrange_Stackable_SameItemMerge_OverwritesExpiryEvenOnPartialMerge()
    {
        var source = Stack(2, 20, gem1: 1, expireDate: 111);
        var destination = Stack(2, 5, gem1: 99, expireDate: 222);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 5, ContainerMatrix.StorePage0, 1,
            source, destination, true, true);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewSource!.Value.Quantity);
        var dest = result.NewDestination!.Value;
        Assert.Equal(10, dest.Quantity);
        Assert.Equal(0, dest.Enchant);
        Assert.Equal(0, dest.Serial);
        Assert.Equal(111, dest.ExpireDate);
    }

    [Fact]
    public void Rearrange_Stackable_SameItemMerge_ClearsSourceWhenFullyDepleted()
    {
        var source = Stack(2, 5);
        var destination = Stack(2, 5);

        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 5, ContainerMatrix.StorePage0, 1,
            source, destination, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        Assert.Equal(10, result.NewDestination!.Value.Quantity);
    }

    [Fact]
    public void Rearrange_Stackable_MergeWouldExceedCap_IsConflict()
    {
        var result = StoreItemTransferPolicy.ResolveRearrangeWithinStore(
            ContainerMatrix.StorePage0, 0, 500, ContainerMatrix.StorePage0, 1,
            Stack(2, 500), Stack(2, 999 - 100), true, true);

        Assert.Equal(StoreItemTransferPolicy.TransferOutcome.DestinationConflict, result.Outcome);
    }
}
