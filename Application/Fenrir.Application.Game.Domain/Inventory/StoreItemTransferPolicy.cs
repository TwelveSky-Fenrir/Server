namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the Store/coffre item-move family (<c>CZ_PROCESS_DATA_SEND</c> tSort
///     223/250 deposit, 224/248 withdraw, 225 store-to-store rearrange). Store reuses <see cref="ContainerMatrix" />
///     's own two-page container ids (<see cref="ContainerMatrix.StorePage0" />/<see cref="ContainerMatrix.StorePage1" />,
///     28 slots each), unlike the flat 28-slot <see cref="SaveBankItemTransferPolicy" />, so slot-range validation
///     for both the inventory and store sides goes through <see cref="ContainerMatrix.IsValidSlot" /> directly.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2540-2650 (<c>ProcessForInventoryToStore</c>) ; :2652-2757
///     (<c>ProcessForStoreToInventory</c>) ; :2759-2901 (<c>ProcessForStoreToStore</c>, including the zero-quantity
///     "move whole stack" substitution and the full-record swap on a mismatched full-stack move) ; :42-53
///     (<c>CopyGSocket</c>) ; :102-110 (<c>ClearStore</c>) ; Server/ts25zone/S04_MyWork04.cpp:441-482, 485-664 (the
///     two-level tSort dispatch, confirming the commented-out "Remote Storage Fix" NPC-proximity checks are dead
///     code in this build) ; Server/Header/Protocol/DEFINE.h:287-296 (<c>MAX_STORE_ITEM_PAGE_NUM</c>=2,
///     <c>MAX_STORE_ITEM_SLOT_NUM</c>=28) ; DEFINE.h:611 (<c>MAX_ITEM_DUPLICATION_NUM</c>=999, reused here via
///     <see cref="GroundItemPickupPolicy.MaxStackQuantity" />) ; Server/Header/Protocol/STRUCT.h:171-212
///     (<c>STORE_WORK</c> confirms storage slots have no x/y field, unlike inventory slots).
///     <para>
///         The second-page access gates (<c>wInventoryDate</c>/<c>wStoreDate</c> vs <c>ReturnNowDate()</c>) are
///         exposed as caller-supplied bools -- the same posture <see cref="SaveBankItemTransferPolicy" /> already
///         uses for its own <c>wInventoryDate</c> gate: neither <c>PlayerRuntimeState</c> nor
///         <c>Fenrir.Data.Abstractions</c> carries either date field yet. Same posture for
///         <c>sourceSupportsSocket</c>: Fenrir's <c>ItemDefinition</c> has no <c>IsValidSocket</c>-equivalent flag
///         yet, so the caller must supply it once that data exists.
///     </para>
///     <para>
///         Empty-slot and "unknown item id" are collapsed into a single caller-supplied <see langword="null" />
///         <c>source</c>/<c>destination</c> -- same posture as <see cref="SaveBankItemTransferPolicy" />: this pure
///         policy never touches an item catalog itself, so the caller must already have resolved the slot's item
///         id against the catalog and pass <see langword="null" /> for either "truly empty" or "catalog lookup
///         failed" (both collapse to the same silent soft failure per the behavior contract).
///     </para>
/// </remarks>
public static class StoreItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        /// <summary>
        ///     Store-to-store rearrange targeting the slot it already occupies -- a no-mutation safe guard, checked
        ///     before any item data is read.
        /// </summary>
        NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

        /// <summary>Withdraw-only: destinationXPost/destinationYPost must each be 0-7.</summary>
        DestinationCoordinateOutOfRange,

        /// <summary>
        ///     The touched second page (either the Inventory side or the Store side) has an expired
        ///     <c>wInventoryDate</c>/<c>wStoreDate</c>. Deposit/withdraw check each side independently (two separate
        ///     gates); rearrange checks a single combined gate (either side being page 2 blocks the whole request).
        /// </summary>
        SecondPageExpired,

        /// <summary>Source slot is empty, or its item id no longer resolves to a known item definition.</summary>
        SourceEmpty,

        /// <summary>
        ///     Stackable transfer: requested quantity is non-positive (other than rearrange's 0-means-whole-stack
        ///     case), &gt; 999, or &gt; the source's current quantity.
        /// </summary>
        InvalidQuantity,

        /// <summary>
        ///     Stackable: destination holds a different item id and (for deposit/withdraw) any amount, or (for
        ///     rearrange) less than the source's entire stack; or the merge would exceed 999. Non-stackable
        ///     deposit/withdraw: destination is already occupied (no implicit swap in those two directions).
        /// </summary>
        DestinationConflict
    }

    /// <summary>Maps <c>DefaultPData</c>'s raw 0/1 store page onto <see cref="ContainerMatrix" />'s StorePage0/StorePage1 ids.</summary>
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

    /// <summary>
    ///     Maps <c>DefaultPData</c>'s raw 0/1 inventory page onto <see cref="ContainerMatrix" />'s
    ///     InventoryPage0/InventoryPage1 ids.
    /// </summary>
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

    /// <summary>tSort 223/250 -- inventory slot to store slot.</summary>
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

    /// <summary>tSort 224/248 -- store slot to inventory slot.</summary>
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

        return ResolveOneWayTransfer(requestedQuantity, src, destination, sourceIsStackable, sourceSupportsSocket);
    }

    /// <summary>
    ///     tSort 225 -- store slot to store slot. Unlike <see cref="ResolveDepositFromInventory" />/
    ///     <see cref="ResolveWithdrawToInventory" />, a mismatched-item destination can still succeed (as a full
    ///     whole-record swap) when the entire source stack is being moved, and a non-stackable source always swaps
    ///     regardless of whether the destination is occupied.
    /// </summary>
    public static TransferResult ResolveRearrangeWithinStore(
        byte fromContainer, int fromSlot, int requestedQuantity,
        byte toContainer, int toSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable,
        bool secondStorePageAccessible)
    {
        if (!ContainerMatrix.IsValidSlot(fromContainer, fromSlot))
            return Fail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(toContainer, toSlot))
            return Fail(TransferOutcome.DestinationOutOfRange);

        // Combined OR -- unlike the two independent gates in deposit/withdraw, rearrange never leaves Store, so
        // there is only one date field to check regardless of which side is page 2.
        if ((fromContainer == ContainerMatrix.StorePage1 || toContainer == ContainerMatrix.StorePage1) &&
            !secondStorePageAccessible)
            return Fail(TransferOutcome.SecondPageExpired);

        if (fromContainer == toContainer && fromSlot == toSlot)
            return new TransferResult(TransferOutcome.NoOp, source, destination, false);

        if (source is not { } src)
            return Fail(TransferOutcome.SourceEmpty);

        if (!sourceIsStackable)
            // Unconditional whole-record swap -- occupied or empty, it never rejects. A same-container swap needs
            // no CopyGSocket-style zeroing (both slots already live in the same socket array), unlike the
            // cross-container copy ResolveOneWayTransfer performs.
            return new TransferResult(TransferOutcome.Success, destination, src, false);

        if (requestedQuantity < 0 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            return Fail(TransferOutcome.InvalidQuantity);

        // Legacy accommodation: 0 means "move the entire source stack" -- the one place among the Store sub-actions
        // where a zero quantity is accepted rather than rejected.
        var effectiveQuantity = requestedQuantity == 0 ? src.Quantity : requestedQuantity;
        if (effectiveQuantity > src.Quantity)
            return Fail(TransferOutcome.InvalidQuantity);

        if (destination is { } dst && dst.ItemId != src.ItemId)
        {
            // A mismatched stack only earns a full swap when the entire source stack moves; a partial move into a
            // different item is an outright rejection, same posture as deposit/withdraw's DestinationConflict.
            if (effectiveQuantity != src.Quantity)
                return Fail(TransferOutcome.DestinationConflict);

            return new TransferResult(TransferOutcome.Success, dst, src, false);
        }

        var mergedQuantity = (destination?.Quantity ?? 0) + effectiveQuantity;
        if (mergedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            return Fail(TransferOutcome.DestinationConflict);

        // Legacy quirk, reproduced not fixed: the expiry-date is overwritten from source unconditionally here, even
        // on a partial merge that leaves the source slot non-empty -- unlike ResolveOneWayTransfer's
        // CopySocketAndExpiry, which only copies once the source is fully depleted.
        var newDestination = (destination ?? new ItemStack(src.ItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)) with
        {
            ItemId = src.ItemId,
            Quantity = mergedQuantity,
            Enchant = 0,
            Combine = 0,
            Refine = 0,
            Socket = 0,
            Serial = 0,
            ExpireDate = src.ExpireDate
        };

        var remaining = src.Quantity - effectiveQuantity;
        var newSource = remaining == 0 ? (ItemStack?)null : src with { Quantity = remaining };

        return new TransferResult(TransferOutcome.Success, newSource, newDestination, false);
    }

    /// <summary>
    ///     The shared stackable/non-stackable transfer core for deposit/withdraw (identical either direction): a
    ///     stackable source merges into a same-item destination (or fills an empty one), forcing the destination's
    ///     packed value/serial to zero on every such addition; a non-stackable source ignores the requested
    ///     quantity entirely and moves whole, rejecting an occupied destination outright. Whenever the source slot
    ///     ends up fully empty (whole non-stackable move, or a stackable transfer that exactly empties the
    ///     remainder), the gem-socket array and rent-expiration date are copied from source to destination
    ///     (gem-socket zeroed instead if <paramref name="sourceSupportsSocket" /> is false) -- a partial stackable
    ///     transfer that leaves quantity behind touches only the raw id/quantity counters, leaving both slots'
    ///     gem-socket/expiration-date fields exactly as they already were.
    /// </summary>
    private static TransferResult ResolveOneWayTransfer(
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

    /// <summary>
    ///     NewSource/NewDestination are the values to write back; null means "slot becomes empty".
    ///     <see cref="IsNonStackableTransfer" /> tells the caller whether this move took the non-stackable
    ///     whole-slot path -- deposit/withdraw only emit their <c>GL_624_STORESLOT_ITEM</c> audit-log call on that
    ///     path (direction 1 for deposit, 2 for withdraw); rearrange never logs regardless and always reports
    ///     <see langword="false" /> here.
    /// </summary>
    public readonly record struct TransferResult(
        TransferOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination,
        bool IsNonStackableTransfer)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success or TransferOutcome.NoOp;
    }
}
