namespace Fenrir.Application.Game.Domain.Inventory;

public static class StoreItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

        DestinationCoordinateOutOfRange,

        SecondPageExpired,

        SourceEmpty,

        InvalidQuantity,

        DestinationConflict
    }

    public static bool TryResolveStoreContainer(int rawPage, out byte container)
    {
        switch (rawPage)
        {
            case 0:
                container = ContainerMatrix.StorePage0;
                return true;
            case 1:
                container = ContainerMatrix.StorePage1;
                return true;
            default:
                container = 0;
                return false;
        }
    }

    public static bool TryResolveInventoryContainer(int rawPage, out byte container)
    {
        switch (rawPage)
        {
            case 0:
                container = ContainerMatrix.InventoryPage0;
                return true;
            case 1:
                container = ContainerMatrix.InventoryPage1;
                return true;
            default:
                container = 0;
                return false;
        }
    }

    public static TransferResult ResolveDepositFromInventory(
        byte inventoryContainer, int inventorySlot, int requestedQuantity,
        byte storeContainer, int storeSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket,
        bool secondInventoryPageAccessible, bool secondStorePageAccessible)
    {
        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(storeContainer, storeSlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (storeContainer == ContainerMatrix.StorePage1 && !secondStorePageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        return ResolveOneWayTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket);
    }

    public static TransferResult ResolveWithdrawToInventory(
        byte storeContainer, int storeSlot, int requestedQuantity,
        byte inventoryContainer, int inventorySlot, int destinationXPost, int destinationYPost,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket,
        bool secondStorePageAccessible, bool secondInventoryPageAccessible)
    {
        if (!ContainerMatrix.IsValidSlot(storeContainer, storeSlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if (destinationXPost is < 0 or > 7 || destinationYPost is < 0 or > 7)
            return Fail(TransferOutcome.DestinationCoordinateOutOfRange);

        if (storeContainer == ContainerMatrix.StorePage1 && !secondStorePageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        return ResolveOneWayTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket,
            (byte)destinationXPost, (byte)destinationYPost);
    }

    public static TransferResult ResolveRearrangeWithinStore(
        byte fromContainer, int fromSlot, int requestedQuantity,
        byte toContainer, int toSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket,
        bool secondStorePageAccessible)
    {
        if (!ContainerMatrix.IsValidSlot(fromContainer, fromSlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(toContainer, toSlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        if ((fromContainer == ContainerMatrix.StorePage1 || toContainer == ContainerMatrix.StorePage1) &&
            !secondStorePageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (fromContainer == toContainer && fromSlot == toSlot)
            return new TransferResult(TransferOutcome.NoOp, source, destination, false);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        if (!sourceIsStackable)
        {
            if (destination is not null)
                return Fail(TransferOutcome.DestinationConflict);

            return new TransferResult(TransferOutcome.Success, null, src, true);
        }

        if (requestedQuantity < 0 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            return Fail(TransferOutcome.InvalidQuantity);

        var effectiveQuantity = requestedQuantity == 0 ? src.Quantity : requestedQuantity;
        if (effectiveQuantity > src.Quantity)
            return Fail(TransferOutcome.InvalidQuantity);

        if (destination is { } dst && dst.ItemId != src.ItemId)
            return Fail(TransferOutcome.DestinationConflict);

        var mergedQuantity = (destination?.Quantity ?? 0) + effectiveQuantity;
        if (mergedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            return Fail(TransferOutcome.DestinationConflict);

        var newDestination = (destination ?? new ItemStack(src.ItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)) with
        {
            ItemId = src.ItemId,
            Quantity = mergedQuantity,
            Enchant = 0,
            Combine = 0,
            Refine = 0,
            Socket = 0,
            Serial = 0
        };

        var remaining = src.Quantity - effectiveQuantity;
        if (remaining > 0)
            return new TransferResult(TransferOutcome.Success, src with { Quantity = remaining }, newDestination,
                false);

        return new TransferResult(TransferOutcome.Success, null,
            CopySocketAndExpiry(newDestination, src, sourceSupportsSocket), false);
    }

    private static TransferResult ResolveOneWayTransfer(
        int requestedQuantity, ItemStack source, ItemStack? destination,
        bool sourceIsStackable, bool sourceSupportsSocket,
        byte destinationX = 0, byte destinationY = 0)
    {
        if (!sourceIsStackable)
        {
            if (destination is not null)
                return Fail(TransferOutcome.DestinationConflict);

            var moved = CopySocketAndExpiry(source, source, sourceSupportsSocket) with
            {
                XPos = destinationX, YPos = destinationY
            };
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

        var newDestination = new ItemStack(source.ItemId, requestedQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            destinationX, destinationY);
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
