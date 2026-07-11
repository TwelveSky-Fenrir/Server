using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class PetBagItemTransferPolicyTests
{
    private const byte EligibleSort = PetBagItemTransferPolicy.PetBagEligibleSort;
    private const byte IneligibleSort = 4;

    private static ItemStack Stack(int itemId, int quantity = 0, byte enchant = 0, byte combine = 0,
        byte refine = 0, byte socket = 0, int serial = 0)
    {
        return new ItemStack(itemId, quantity, enchant, combine, refine, socket, 0, 0, 0, 0, serial);
    }


    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    public void IsValidBagSlot_Bounds_20Slots(int slot, bool expected)
    {
        Assert.Equal(expected, PetBagItemTransferPolicy.IsValidBagSlot(slot));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(19, true)]
    public void RequiresUpperHalfEntitlement_SlotsAtOrAbove10(int slot, bool expected)
    {
        Assert.Equal(expected, PetBagItemTransferPolicy.RequiresUpperHalfEntitlement(slot));
    }


    [Fact]
    public void Deposit_SourceOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 64, 0,
            Stack(1, serial: 1), null, EligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Deposit_DestinationOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 20,
            Stack(1), null, EligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Fact]
    public void Deposit_PetNotEquipped_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            Stack(1), null, EligibleSort,
            false, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetNotEquipped, result.Outcome);
    }

    [Fact]
    public void Deposit_UpperHalfSlotWithoutEntitlement_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 10,
            Stack(1), null, EligibleSort,
            true, false, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetBagUpperHalfExpired, result.Outcome);
    }

    [Fact]
    public void Deposit_LowerHalfSlot_IgnoresUpperHalfGate()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 9,
            Stack(1), null, EligibleSort,
            true, false, true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Deposit_SecondInventoryPageNotAccessible_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage1, 0, 0,
            Stack(1), null, EligibleSort,
            true, true, false);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SecondInventoryPageExpired, result.Outcome);
    }

    [Fact]
    public void Deposit_FirstInventoryPage_IgnoresSecondPageGate()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            Stack(1), null, EligibleSort,
            true, true, false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Deposit_EmptySource_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            null, null, EligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void Deposit_SourceNotAtRest_Fails(int quantity, int enchant, int combine, int refine, int socket)
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            Stack(1, quantity, (byte)enchant, (byte)combine, (byte)refine, (byte)socket), null, EligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceNotAtRest, result.Outcome);
    }

    [Fact]
    public void Deposit_SourceAtRest_ChecksCatalogSortNext()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            Stack(1), null, IneligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceItemNotPetEligible, result.Outcome);
    }

    [Fact]
    public void Deposit_DestinationOccupied_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 0,
            Stack(1), 999, EligibleSort,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOccupied, result.Outcome);
    }

    [Fact]
    public void Deposit_Success_ClearsGeneralInventorySlotAndCopiesOnlyCatalogId()
    {
        var source = Stack(777, serial: 42);

        var result = PetBagItemTransferPolicy.ResolveDepositFromInventory(
            ContainerMatrix.InventoryPage0, 0, 5,
            source, null, EligibleSort,
            true, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewGeneralInventorySlot);
        Assert.Equal(777, result.NewPetBagItemId);
        Assert.True(result.ShouldEmitAuditLog);
    }


    [Fact]
    public void Withdraw_SourceOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            20, 1,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 0,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
    }

    [Fact]
    public void Withdraw_DestinationOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage0, 64, 0, 0,
            null, 0,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 8)]
    public void Withdraw_DestinationCoordinatesOutOfRange_Fails(int x, int y)
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage0, 0, x, y,
            null, 0,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationCoordinateOutOfRange, result.Outcome);
    }

    [Fact]
    public void Withdraw_PetNotEquipped_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 0,
            false, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetNotEquipped, result.Outcome);
    }

    [Fact]
    public void Withdraw_SourceUpperHalfSlotWithoutEntitlement_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            10, 1,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 0,
            true, false, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetBagUpperHalfExpired, result.Outcome);
    }

    [Fact]
    public void Withdraw_SecondInventoryPageNotAccessible_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage1, 0, 0, 0,
            null, 0,
            true, true, false);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SecondInventoryPageExpired, result.Outcome);
    }

    [Fact]
    public void Withdraw_EmptySource_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, null,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 0,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Fact]
    public void Withdraw_DestinationOccupied_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            Stack(999), 0,
            true, true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOccupied, result.Outcome);
    }

    [Fact]
    public void Withdraw_Success_BuildsBareStackAtZeroQuantityAndValue_ClearsBagSlot()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            3, 555,
            ContainerMatrix.InventoryPage0, 0, 2, 4,
            null, 0,
            true, true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSourcePetBagItemId);
        Assert.True(result.ShouldEmitAuditLog);

        var dest = result.NewGeneralInventorySlot!.Value;
        Assert.Equal(555, dest.ItemId);
        Assert.Equal(0, dest.Quantity);
        Assert.Equal(0, dest.Enchant);
        Assert.Equal(0, dest.Combine);
        Assert.Equal(0, dest.Refine);
        Assert.Equal(0, dest.Socket);
        Assert.Equal(0, dest.SocketGem1);
        Assert.Equal(0, dest.ExpireDate);
        Assert.Equal(0, dest.Serial);
    }

    [Fact]
    public void Withdraw_Success_WritesCallerSuppliedSerialNumber()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            3, 555,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 12345,
            true, true, true);

        Assert.Equal(12345, result.NewGeneralInventorySlot!.Value.Serial);
    }

    [Fact]
    public void Withdraw_DoesNotValidateCatalogSortCode_UnlikeDepositAndRearrange()
    {
        var result = PetBagItemTransferPolicy.ResolveWithdrawToInventory(
            0, 1,
            ContainerMatrix.InventoryPage0, 0, 0, 0,
            null, 0,
            true, true, true);

        Assert.True(result.Succeeded);
    }


    [Fact]
    public void Rearrange_SameSlot_IsNoOp_BypassingEveryOtherGuard()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            5, 5,
            10, 10, IneligibleSort,
            false, false);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.NoOp, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal(10, result.NewSourcePetBagItemId);
        Assert.Equal(10, result.NewDestinationPetBagItemId);
    }

    [Fact]
    public void Rearrange_SourceOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            20, 0,
            1, null, EligibleSort,
            true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceOutOfRange, result.Outcome);
    }

    [Fact]
    public void Rearrange_DestinationOutOfRange_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 20,
            1, null, EligibleSort,
            true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOutOfRange, result.Outcome);
    }

    [Fact]
    public void Rearrange_PetNotEquipped_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 1,
            1, null, EligibleSort,
            false, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetNotEquipped, result.Outcome);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    public void Rearrange_EitherSideUpperHalfWithoutEntitlement_Fails(int sourceSlot, int destinationSlot)
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            sourceSlot, destinationSlot,
            1, null, EligibleSort,
            true, false);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.PetBagUpperHalfExpired, result.Outcome);
    }

    [Fact]
    public void Rearrange_EmptySource_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 1,
            null, null, EligibleSort,
            true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceEmpty, result.Outcome);
    }

    [Fact]
    public void Rearrange_SourceNotPetEligible_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 1,
            1, null, IneligibleSort,
            true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.SourceItemNotPetEligible, result.Outcome);
    }

    [Fact]
    public void Rearrange_DestinationOccupied_Fails()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 1,
            1, 999, EligibleSort,
            true, true);

        Assert.Equal(PetBagItemTransferPolicy.TransferOutcome.DestinationOccupied, result.Outcome);
    }

    [Fact]
    public void Rearrange_Success_MovesCatalogIdAndClearsSource()
    {
        var result = PetBagItemTransferPolicy.ResolveRearrangeWithinPetBag(
            0, 1,
            777, null, EligibleSort,
            true, true);

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSourcePetBagItemId);
        Assert.Equal(777, result.NewDestinationPetBagItemId);
    }
}
