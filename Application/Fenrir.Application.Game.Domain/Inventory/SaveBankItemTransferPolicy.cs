namespace Fenrir.Application.Game.Domain.Inventory;

public static class SaveBankItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

                NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

                DestinationCoordinateOutOfRange,

                SecondInventoryPageExpired,

                SourceEmpty,

                SourceItemBlocked,

                InvalidQuantity,

                DestinationConflict
    }

        public const int SlotCount = 28;

    public const int MaxSlotInclusive = SlotCount - 1;

        private const int DepositBlockedItemId = 8290;

    public static bool IsValidSlot(int slot)
    {
        return slot is >= 0 and <= MaxSlotInclusive;
    }

        public static TransferResult ResolveDepositFromInventory(
        byte inventoryContainer, int inventorySlot, int requestedQuantity, int bankSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket, bool secondInventoryPageAccessible)
    {
        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!IsValidSlot(bankSlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return Fail(TransferOutcome.SecondInventoryPageExpired);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        if (src.ItemId == DepositBlockedItemId)
            return Fail(TransferOutcome.SourceItemBlocked);

        return ResolveTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket);
    }

        public static TransferResult ResolveWithdrawToInventory(
        int bankSlot, int requestedQuantity, byte inventoryContainer, int inventorySlot,
        int destinationXPost, int destinationYPost,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket, bool secondInventoryPageAccessible)
    {
        if (!IsValidSlot(bankSlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if (destinationXPost is < 0 or > 7 || destinationYPost is < 0 or > 7)
            return Fail(TransferOutcome.DestinationCoordinateOutOfRange);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return Fail(TransferOutcome.SecondInventoryPageExpired);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        return ResolveTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket);
    }

        public static TransferResult ResolveRearrangeWithinBank(
        int sourceBankSlot, int requestedQuantity, int destinationBankSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket)
    {
        if (!IsValidSlot(sourceBankSlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!IsValidSlot(destinationBankSlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if (sourceBankSlot == destinationBankSlot)
            return new TransferResult(TransferOutcome.NoOp, source, destination, false);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        return ResolveTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket);
    }

        private static TransferResult ResolveTransfer(
        int requestedQuantity, ItemStack source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket)
    {
        if (!sourceIsStackable)
        {
            if (destination is not null)
                return Fail(TransferOutcome.DestinationConflict);

            var moved = CopySocketAndExpiry(source, source, sourceSupportsSocket);
            return new TransferResult(TransferOutcome.Success, null, moved, true);
        }

        if (requestedQuantity <= 0 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity ||
            requestedQuantity > source.Quantity)
            return Fail(TransferOutcome.InvalidQuantity);

        if (destination is { } dst)
        {
            if (dst.ItemId != source.ItemId)
                return Fail(TransferOutcome.DestinationConflict);

            var merged = dst.Quantity + requestedQuantity;
            if (merged > GroundItemPickupPolicy.MaxStackQuantity)
                return Fail(TransferOutcome.DestinationConflict);

            var mergedDestination = dst with
            {
                Quantity = merged, Enchant = 0, Combine = 0, Refine = 0, Socket = 0, Serial = 0
            };

            var remainingAfterMerge = source.Quantity - requestedQuantity;
            if (remainingAfterMerge > 0)
                return new TransferResult(TransferOutcome.Success, source with { Quantity = remainingAfterMerge },
                    mergedDestination, false);

            return new TransferResult(TransferOutcome.Success, null,
                CopySocketAndExpiry(mergedDestination, source, sourceSupportsSocket), false);
        }

        var newDestination = new ItemStack(source.ItemId, requestedQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var remaining = source.Quantity - requestedQuantity;
        if (remaining > 0)
            return new TransferResult(TransferOutcome.Success, source with { Quantity = remaining }, newDestination,
                false);

        return new TransferResult(TransferOutcome.Success, null,
            CopySocketAndExpiry(newDestination, source, sourceSupportsSocket), false);
    }

    private static ItemStack CopySocketAndExpiry(ItemStack destination, ItemStack source, bool sourceSupportsSocket)
    {
        return destination with
        {
            SocketGem1 = sourceSupportsSocket ? source.SocketGem1 : 0,
            SocketGem2 = sourceSupportsSocket ? source.SocketGem2 : 0,
            SocketGem3 = sourceSupportsSocket ? source.SocketGem3 : 0,
            ExpireDate = source.ExpireDate
        };
    }

    private static TransferResult Fail(TransferOutcome outcome)
    {
        return new TransferResult(outcome, null, null, false);
    }

        public readonly record struct TransferResult(
        TransferOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination,
        bool IsNonStackableTransfer)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success or TransferOutcome.NoOp;
    }
}
