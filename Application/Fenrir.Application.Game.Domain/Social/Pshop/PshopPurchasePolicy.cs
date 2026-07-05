using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

/// <summary>
///     Pure, Zone-independent policy for the live personal-shop-stall family (CZ_START/BUY_PSHOP_SEND,
///     S04_MyWork02.cpp:6021-7124). PshopInfo.ItemInfo is a flat int[225] = [5 pages][5 slots][9 fields]
///     row-major; <see cref="FlatIndex" /> is the one place indexing math lives.
/// </summary>
public static class PshopPurchasePolicy
{
    public enum OpenSlotOutcome
    {
        Success,
        UnknownItem,
        PriceOutOfRange,
        InvalidStackQuantity,

        /// <summary>
        ///     The declared (page,index) inventory slot no longer holds the exact advertised (id,quantity,value) -- stale
        ///     client state.
        /// </summary>
        InventoryMismatch
    }

    public enum PurchaseOutcome
    {
        Success,

        /// <summary>
        ///     Destination occupied by an incompatible item, or a merge would exceed MaxStackQuantity -- Quit()-worthy in the
        ///     legacy, no clean fail path.
        /// </summary>
        DestinationConflict
    }

    public const int MaxPages = 5;
    public const int MaxSlots = 5;
    public const int FieldsPerSlot = 9;

    /// <summary>(sell.price &lt; 1) || (sell.price &gt; ((MAX_NUMBER_SIZE / 2) - 1)) (S04_MyWork02.cpp:6170).</summary>
    public const int MaxSellPrice = 999_999_999;

    public static int FlatIndex(int page, int slot)
    {
        return (page * MaxSlots + slot) * FieldsPerSlot;
    }

    public static SlotView ReadSlot(PshopInfo info, int page, int slot)
    {
        var i = FlatIndex(page, slot);
        var a = info.ItemInfo;
        return new SlotView(a[i], a[i + 1], a[i + 2], a[i + 3], a[i + 4], a[i + 5], a[i + 6], a[i + 7], a[i + 8]);
    }

    /// <summary>
    ///     iCheckAvatarShop (item barred from personal-shop sale) is not modeled -- no such field exists on ItemRowDto
    ///     yet.
    /// </summary>
    public static OpenSlotOutcome ValidateOpenSlot(SlotView slot, ItemDefinition? itemDefinition, ItemStack? liveSlot)
    {
        if (itemDefinition is null)
            return OpenSlotOutcome.UnknownItem;

        if (slot.Price is < 1 or > MaxSellPrice)
            return OpenSlotOutcome.PriceOutOfRange;

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (isStackable && slot.Quantity < 1)
            return OpenSlotOutcome.InvalidStackQuantity;

        if (liveSlot is not { } live || live.ItemId != slot.ItemId || live.Quantity != slot.Quantity ||
            live.Value() != slot.Value)
            return OpenSlotOutcome.InventoryMismatch;

        return OpenSlotOutcome.Success;
    }

    /// <summary>
    ///     Buyer-destination half of BUY_PSHOP_SEND (S04_MyWork02.cpp:7021-7051): merges into a same-item
    ///     stackable destination or fills an empty slot; a non-stackable/mismatched occupied destination is a
    ///     hard reject (the source Quit()s here, no swap fallback).
    /// </summary>
    public static PurchaseResult ResolvePurchase(SlotView listing, ItemDefinition itemDefinition,
        ItemStack? destinationSlot)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(listing.Value);

        if (destinationSlot is { } existing)
        {
            if (existing.ItemId != listing.ItemId || !ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort))
                return new PurchaseResult(PurchaseOutcome.DestinationConflict, null);

            var merged = existing.Quantity + listing.Quantity;
            if (merged > GroundItemPickupPolicy.MaxStackQuantity)
                return new PurchaseResult(PurchaseOutcome.DestinationConflict, null);

            return new PurchaseResult(PurchaseOutcome.Success, existing with { Quantity = merged });
        }

        var newStack = new ItemStack(listing.ItemId, listing.Quantity, enchant, combine, refine, socket, 0, 0, 0, 0,
            listing.Serial);
        return new PurchaseResult(PurchaseOutcome.Success, newStack);
    }

    public readonly record struct SlotView(
        int ItemId,
        int Quantity,
        int Value,
        int Serial,
        int Price,
        int InventoryPage,
        int InventoryIndex,
        int PosX,
        int PosY)
    {
        public bool IsOccupied => ItemId >= 1;
    }

    public readonly record struct PurchaseResult(PurchaseOutcome Outcome, ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == PurchaseOutcome.Success;
    }
}

/// <summary>ItemStack has no single packed Value field of its own; PSHOP_INFO's Value field is that same packed encoding.</summary>
public static class ItemStackValueExtensions
{
    public static int Value(this ItemStack stack)
    {
        return ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
    }
}
