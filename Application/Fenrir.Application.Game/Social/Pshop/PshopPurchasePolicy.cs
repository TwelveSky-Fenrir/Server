using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Social.Pshop;

/// <summary>
///     Pure, Zone-independent policy for the LIVE personal-shop-stall family (CZ_START/BUY_PSHOP_SEND,
///     contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:6021-7124</c>). <see cref="PshopInfo.ItemInfo" />
///     is a flat <c>int[225]</c> = <c>[5 pages][5 slots][9 fields]</c> row-major (see that type's own
///     remarks for the 9-field layout) -- <see cref="FlatIndex" /> is the one place that indexing math
///     lives, everything else in this type and its callers goes through it.
/// </summary>
public static class PshopPurchasePolicy
{
    public const int MaxPages = 5;
    public const int MaxSlots = 5;
    public const int FieldsPerSlot = 9;

    /// <summary><c>(sell.price &lt; 1) || (sell.price &gt; ((MAX_NUMBER_SIZE / 2) - 1))</c> (S04_MyWork02.cpp:6170).</summary>
    public const int MaxSellPrice = 999_999_999;

    public static int FlatIndex(int page, int slot)
    {
        return (page * MaxSlots + slot) * FieldsPerSlot;
    }

    /// <summary>One decoded PSHOP_INFO slot -- see <see cref="PshopInfo" />'s own field-layout remarks.</summary>
    public readonly record struct SlotView(
        int ItemId, int Quantity, int Value, int Serial, int Price,
        int InventoryPage, int InventoryIndex, int PosX, int PosY)
    {
        public bool IsOccupied => ItemId >= 1;
    }

    public static SlotView ReadSlot(PshopInfo info, int page, int slot)
    {
        var i = FlatIndex(page, slot);
        var a = info.ItemInfo;
        return new SlotView(a[i], a[i + 1], a[i + 2], a[i + 3], a[i + 4], a[i + 5], a[i + 6], a[i + 7], a[i + 8]);
    }

    public enum OpenSlotOutcome
    {
        Success,
        UnknownItem,
        PriceOutOfRange,
        InvalidStackQuantity,

        /// <summary>The declared (page,index) inventory slot no longer holds the exact advertised (id,quantity,value) -- stale client state.</summary>
        InventoryMismatch
    }

    /// <summary>
    ///     Validates ONE occupied slot of a submitted PSHOP_INFO at open time against the seller's LIVE
    ///     inventory (S04_MyWork02.cpp:6140-6305, the always-taken "else" branch -- Fenrir's non-preloading
    ///     proxy flow means the <c>sell.page==-1</c> preload-only branch never applies here, see
    ///     <see cref="PlayerRuntimeState.PshopOpen" />'s own remarks). <c>iCheckAvatarShop</c> (item barred
    ///     from personal-shop sale) is NOT modeled -- no such field exists on Fenrir's <see cref="ItemRowDto" />
    ///     yet (documented open issue, not a guess).
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

    public enum PurchaseOutcome
    {
        Success,

        /// <summary>Destination occupied by an incompatible item, or a stack merge would exceed <see cref="GroundItemPickupPolicy.MaxStackQuantity" /> -- Quit()-worthy per the verified source (no clean fail path for this specific case).</summary>
        DestinationConflict
    }

    public readonly record struct PurchaseResult(PurchaseOutcome Outcome, ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == PurchaseOutcome.Success;
    }

    /// <summary>
    ///     Ports the buyer-destination half of <c>BUY_PSHOP_SEND</c> (S04_MyWork02.cpp:7021-7051) --
    ///     merges into a same-item stackable destination (bounded <see cref="GroundItemPickupPolicy.MaxStackQuantity" />)
    ///     or fills an empty slot; a non-stackable/mismatched occupied destination is a hard reject (the
    ///     verified source Quit()s here, no swap fallback).
    /// </summary>
    public static PurchaseResult ResolvePurchase(SlotView listing, ItemDefinition itemDefinition, ItemStack? destinationSlot)
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
}

/// <summary>Small helper: <see cref="ItemStack" /> has no single packed "Value" field of its own (it already decoded enchant/combine/refine/socket) -- PSHOP_INFO's own <c>Value</c> field is that SAME packed encoding, so this re-encodes for the open-time comparison.</summary>
internal static class ItemStackValueExtensions
{
    public static int Value(this ItemStack stack)
    {
        return ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
    }
}
