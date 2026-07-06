namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the Save/Bank item-move family (<c>CZ_PROCESS_DATA_SEND</c> tSort 228/251
///     deposit, 229/249 withdraw, 230 bank-to-bank rearrange). Save/Bank is a 28-slot container distinct from every
///     container <see cref="ContainerMatrix" /> already models (not a page/slot pair -- see
///     <see cref="IsValidSlot" />), so this type deliberately does not extend <see cref="ContainerMatrix" />'s own
///     container-id scheme; it operates on <see cref="ItemStack" /> values directly and is unopinionated about how a
///     caller stores the 28 bank slots.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2971-3273 (<c>ProcessForInventoryToSave</c>,
///     <c>ProcessForSaveToInventory</c>, <c>ProcessForSaveToSave</c>) ; Server/Header/Protocol/DEFINE.h:313
///     (<c>MAX_SAVE_ITEM_SLOT_NUM</c> = 28) ; DEFINE.h:611 (<c>MAX_ITEM_DUPLICATION_NUM</c> = 999, reused here via
///     <see cref="GroundItemPickupPolicy.MaxStackQuantity" />) ; Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:286-297
///     (catalog lookup fails for item id &lt; 1, i.e. an empty slot) ; S04_MyWork05.cpp:3005-3009 (deposit-only
///     item-id-8290 block) ; S04_MyWork05.cpp:3096-3101 (withdraw-only destination X/Y range) ;
///     S04_MyWork05.cpp:2985-2993 / :3103-3111 (second-inventory-page access gate on deposit/withdraw respectively).
///     <para>
///         The second-inventory-page gate (<c>wInventoryDate</c> vs <c>ReturnNowDate()</c>) is exposed here as a
///         caller-supplied bool because Fenrir has no <c>wInventoryDate</c>-equivalent field anywhere yet (not on
///         <c>PlayerRuntimeState</c>, not in <c>Fenrir.Data.Abstractions</c>) -- a pre-existing gap that also affects
///         the already-implemented 208/210/213 sorts, not something to invent here. Same posture for
///         <paramref name="sourceSupportsSocket" />: Fenrir's <c>ItemDefinition</c> has no <c>IsValidSocket</c>-equivalent
///         flag yet, so the caller must supply it once that data exists.
///     </para>
/// </remarks>
public static class SaveBankItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        /// <summary>
        ///     Bank-to-bank rearrange targeting the slot it already occupies -- a no-mutation safe guard mirroring
        ///     <see cref="ContainerMatrix.MoveOutcome.NoOp" />'s same-slot precedent; not itself sourced from the cited
        ///     range, which does not describe this input.
        /// </summary>
        NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

        /// <summary>Withdraw-only: tXPost2/tYPost2 must each be 0-7 (S04_MyWork05.cpp:3096-3101).</summary>
        DestinationCoordinateOutOfRange,

        /// <summary>wInventoryDate has expired for the touched second inventory page (deposit/withdraw only).</summary>
        SecondInventoryPageExpired,

        /// <summary>Source slot's raw item id is empty (0) / fails catalog lookup (id &lt; 1).</summary>
        SourceEmpty,

        /// <summary>Deposit-only: source item id is exactly 8290.</summary>
        SourceItemBlocked,

        /// <summary>Stackable transfer: requested quantity is non-positive, &gt; 999, or &gt; the source's current quantity.</summary>
        InvalidQuantity,

        /// <summary>
        ///     Stackable: destination holds a different item id, or the merge would exceed 999. Non-stackable:
        ///     destination is already occupied (no partial/merge concept for non-stackable items).
        /// </summary>
        DestinationConflict
    }

    /// <summary>MAX_SAVE_ITEM_SLOT_NUM (DEFINE.h:313).</summary>
    public const int SlotCount = 28;

    public const int MaxSlotInclusive = SlotCount - 1;

    /// <summary>Deposit-item-only hard block (S04_MyWork05.cpp:3005-3009); not present for withdraw or rearrange.</summary>
    private const int DepositBlockedItemId = 8290;

    public static bool IsValidSlot(int slot)
    {
        return slot is >= 0 and <= MaxSlotInclusive;
    }

    /// <summary>tSort 228/251 -- inventory slot to bank slot.</summary>
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

    /// <summary>tSort 229/249 -- bank slot to inventory slot.</summary>
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

    /// <summary>tSort 230 -- bank slot to bank slot.</summary>
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

    /// <summary>
    ///     The shared stackable/non-stackable transfer core, identical across deposit/withdraw/rearrange
    ///     (S04_MyWork05.cpp:2971-3273): a stackable source merges into a same-item destination (or fills an empty
    ///     one), forcing the destination's packed value/serial to zero on every such addition; a non-stackable
    ///     source ignores the requested quantity entirely and moves whole, rejecting an occupied destination
    ///     outright. Whenever the source slot ends up fully empty (whole non-stackable move, or a stackable
    ///     transfer that exactly empties the remainder), the gem-socket array and rent-expiration date are copied
    ///     from source to destination (gem-socket zeroed instead if <paramref name="sourceSupportsSocket" /> is
    ///     false) -- a partial stackable transfer that leaves quantity behind touches only the raw id/quantity
    ///     counters, leaving both slots' gem-socket/expiration-date fields exactly as they already were.
    /// </summary>
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

    /// <summary>
    ///     NewSource/NewDestination are the values to write back; null means "slot becomes empty".
    ///     <see cref="IsNonStackableTransfer" /> tells the caller whether this move took the non-stackable
    ///     whole-slot path -- deposit/withdraw only emit their <c>GL_626_SAVESLOT_ITEM</c> audit-log call on that
    ///     path (S04_MyWork05.cpp:3061 deposit action 1, :3169 withdraw action 2); rearrange never logs regardless.
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
