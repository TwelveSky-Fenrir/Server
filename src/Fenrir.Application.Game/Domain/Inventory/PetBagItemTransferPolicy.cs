namespace Fenrir.Application.Game.Domain.Inventory;

public static class PetBagItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

        DestinationCoordinateOutOfRange,

        PetNotEquipped,

        PetBagUpperHalfExpired,

        SecondInventoryPageExpired,

        SourceEmpty,

        SourceNotAtRest,

        SourceItemNotPetEligible,

        DestinationOccupied
    }

    public const int BagSlotCount = 20;

    public const int MaxBagSlotInclusive = BagSlotCount - 1;

    public const int UpperHalfStartSlot = 10;

    public const byte PetBagEligibleSort = 3;

    public static bool IsValidBagSlot(int slot)
    {
        return slot is >= 0 and <= MaxBagSlotInclusive;
    }

    public static bool RequiresUpperHalfEntitlement(int bagSlot)
    {
        return bagSlot >= UpperHalfStartSlot;
    }

    public static DepositResult ResolveDepositFromInventory(
        byte inventoryContainer, int inventorySlot, int petBagSlot,
        ItemStack? source, int? destinationBagItemId, byte sourceItemSort,
        bool petEquipped, bool bagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive)
    {
        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return DepositFail(TransferOutcome.SourceOutOfRange);

        if (!IsValidBagSlot(petBagSlot))
            return DepositFail(TransferOutcome.DestinationOutOfRange);

        if (!petEquipped)
            return DepositFail(TransferOutcome.PetNotEquipped);

        if (RequiresUpperHalfEntitlement(petBagSlot) && !bagUpperHalfEntitlementActive)
            return DepositFail(TransferOutcome.PetBagUpperHalfExpired);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageEntitlementActive)
            return DepositFail(TransferOutcome.SecondInventoryPageExpired);

        if (source is not { } src)
            return DepositFail(TransferOutcome.SourceEmpty);

        if (src.Quantity > 0 || src.Enchant != 0 || src.Combine != 0 || src.Refine != 0 || src.Socket != 0)
            return DepositFail(TransferOutcome.SourceNotAtRest);

        if (sourceItemSort != PetBagEligibleSort)
            return DepositFail(TransferOutcome.SourceItemNotPetEligible);

        if (destinationBagItemId is not null)
            return DepositFail(TransferOutcome.DestinationOccupied);

        return new DepositResult(TransferOutcome.Success, null, src.ItemId, true);
    }

    public static WithdrawResult ResolveWithdrawToInventory(
        int sourceBagSlot, int? sourceBagItemId,
        byte destinationInventoryContainer, int destinationInventorySlot, int destinationX, int destinationY,
        ItemStack? destination, int newSerialNumber,
        bool petEquipped, bool bagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive)
    {
        if (!IsValidBagSlot(sourceBagSlot))
            return WithdrawFail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(destinationInventoryContainer, destinationInventorySlot))
            return WithdrawFail(TransferOutcome.DestinationOutOfRange);

        if (destinationX is < 0 or > 7 || destinationY is < 0 or > 7)
            return WithdrawFail(TransferOutcome.DestinationCoordinateOutOfRange);

        if (!petEquipped)
            return WithdrawFail(TransferOutcome.PetNotEquipped);

        if (RequiresUpperHalfEntitlement(sourceBagSlot) && !bagUpperHalfEntitlementActive)
            return WithdrawFail(TransferOutcome.PetBagUpperHalfExpired);

        if (destinationInventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageEntitlementActive)
            return WithdrawFail(TransferOutcome.SecondInventoryPageExpired);

        if (sourceBagItemId is not { } itemId)
            return WithdrawFail(TransferOutcome.SourceEmpty);

        if (destination is not null)
            return WithdrawFail(TransferOutcome.DestinationOccupied);

        var newSlot = new ItemStack(itemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, newSerialNumber,
            (byte)destinationX, (byte)destinationY);
        return new WithdrawResult(TransferOutcome.Success, null, newSlot, true);
    }

    public static RearrangeResult ResolveRearrangeWithinPetBag(
        int sourceBagSlot, int destinationBagSlot,
        int? sourceBagItemId, int? destinationBagItemId, byte sourceItemSort,
        bool petEquipped, bool bagUpperHalfEntitlementActive)
    {
        if (!IsValidBagSlot(sourceBagSlot))
            return RearrangeFail(TransferOutcome.SourceOutOfRange);

        if (!IsValidBagSlot(destinationBagSlot))
            return RearrangeFail(TransferOutcome.DestinationOutOfRange);

        if (sourceBagSlot == destinationBagSlot)
            return new RearrangeResult(TransferOutcome.NoOp, sourceBagItemId, destinationBagItemId);

        if (!petEquipped)
            return RearrangeFail(TransferOutcome.PetNotEquipped);

        if ((RequiresUpperHalfEntitlement(sourceBagSlot) || RequiresUpperHalfEntitlement(destinationBagSlot)) &&
            !bagUpperHalfEntitlementActive)
            return RearrangeFail(TransferOutcome.PetBagUpperHalfExpired);

        if (sourceBagItemId is not { } itemId)
            return RearrangeFail(TransferOutcome.SourceEmpty);

        if (sourceItemSort != PetBagEligibleSort)
            return RearrangeFail(TransferOutcome.SourceItemNotPetEligible);

        if (destinationBagItemId is not null)
            return RearrangeFail(TransferOutcome.DestinationOccupied);

        return new RearrangeResult(TransferOutcome.Success, null, itemId);
    }

    private static DepositResult DepositFail(TransferOutcome outcome)
    {
        return new DepositResult(outcome, null, 0, false);
    }

    private static WithdrawResult WithdrawFail(TransferOutcome outcome)
    {
        return new WithdrawResult(outcome, null, null, false);
    }

    private static RearrangeResult RearrangeFail(TransferOutcome outcome)
    {
        return new RearrangeResult(outcome, null, null);
    }

    public readonly record struct DepositResult(
        TransferOutcome Outcome,
        ItemStack? NewGeneralInventorySlot,
        int NewPetBagItemId,
        bool ShouldEmitAuditLog)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success;
    }

    public readonly record struct WithdrawResult(
        TransferOutcome Outcome,
        int? NewSourcePetBagItemId,
        ItemStack? NewGeneralInventorySlot,
        bool ShouldEmitAuditLog)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success;
    }

    public readonly record struct RearrangeResult(
        TransferOutcome Outcome,
        int? NewSourcePetBagItemId,
        int? NewDestinationPetBagItemId)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success or TransferOutcome.NoOp;
    }
}
