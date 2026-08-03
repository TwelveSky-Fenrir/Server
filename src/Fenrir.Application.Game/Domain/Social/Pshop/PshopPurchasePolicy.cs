using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

public static class PshopPurchasePolicy
{
    public enum OpenSlotOutcome
    {
        Success,
        UnknownItem,

        BarredFromShopSale,

        PriceOutOfRange,
        InvalidStackQuantity,

        InventoryMismatch
    }

    public enum PurchaseOutcome
    {
        Success,

        DestinationConflict
    }

    public const int MaxPages = 5;
    public const int MaxSlots = 5;
    public const int FieldsPerSlot = 9;
    public const int SocketsPerSlot = 3;

    public const int MaxSellPrice = 999_999_999;

    public static int FlatIndex(int page, int slot)
    {
        return (page * MaxSlots + slot) * FieldsPerSlot;
    }

    public static int SocketFlatIndex(int page, int slot)
    {
        return (page * MaxSlots + slot) * SocketsPerSlot;
    }

    public static SlotView ReadSlot(PshopInfo info, int page, int slot)
    {
        var i = FlatIndex(page, slot);
        var s = SocketFlatIndex(page, slot);
        var a = info.ItemInfo;
        var g = info.SocketInfo;
        return new SlotView(a[i], a[i + 1], a[i + 2], a[i + 3], a[i + 4], a[i + 5], a[i + 6], a[i + 7], a[i + 8],
            g[s], g[s + 1], g[s + 2]);
    }

    public static string? EncodeSocketData(int gem1, int gem2, int gem3)
    {
        return gem1 == 0 && gem2 == 0 && gem3 == 0 ? null : $"{gem1},{gem2},{gem3}";
    }

    public static (int Gem1, int Gem2, int Gem3) DecodeSocketData(string? socketData)
    {
        if (string.IsNullOrEmpty(socketData))
            return (0, 0, 0);

        Span<Range> parts = stackalloc Range[SocketsPerSlot];
        var span = socketData.AsSpan();
        if (span.Split(parts, ',') != SocketsPerSlot)
            return (0, 0, 0);

        return int.TryParse(span[parts[0]], out var gem1) && int.TryParse(span[parts[1]], out var gem2) &&
               int.TryParse(span[parts[2]], out var gem3)
            ? (gem1, gem2, gem3)
            : (0, 0, 0);
    }

    public static OpenSlotOutcome ValidateOpenSlot(SlotView slot, ItemDefinition? itemDefinition, ItemStack? liveSlot)
    {
        if (itemDefinition is null)
            return OpenSlotOutcome.UnknownItem;

        if (itemDefinition.Item.CheckAvatarShop == 1)
            return OpenSlotOutcome.BarredFromShopSale;

        if (slot.Price is < 1 or > MaxSellPrice)
            return OpenSlotOutcome.PriceOutOfRange;

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (isStackable && slot.Quantity < 1)
            return OpenSlotOutcome.InvalidStackQuantity;

        if (liveSlot is not { } live)
            return OpenSlotOutcome.InventoryMismatch;

        if (live.ItemId != slot.ItemId || live.Quantity != slot.Quantity || live.Value() != slot.Value)
            return OpenSlotOutcome.InventoryMismatch;

        return OpenSlotOutcome.Success;
    }

    public static PurchaseResult ResolvePurchase(SlotView listing, ItemDefinition itemDefinition,
        ItemStack? destinationSlot, byte destinationX, byte destinationY, int socketGem1 = 0, int socketGem2 = 0,
        int socketGem3 = 0)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(listing.Value);

        if (destinationSlot is { } existing)
        {
            if (existing.ItemId != listing.ItemId || !ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort))
                return new PurchaseResult(PurchaseOutcome.DestinationConflict, null);

            var merged = existing.Quantity + listing.Quantity;
            if (merged > GroundItemPickupPolicy.MaxStackQuantity)
                return new PurchaseResult(PurchaseOutcome.DestinationConflict, null);

            return new PurchaseResult(PurchaseOutcome.Success,
                existing with { Quantity = merged, XPos = destinationX, YPos = destinationY });
        }

        var newStack = new ItemStack(listing.ItemId, listing.Quantity, enchant, combine, refine, socket,
            socketGem1, socketGem2, socketGem3, 0, listing.Serial, destinationX, destinationY);
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
        int PosY,
        int SocketGem1 = 0,
        int SocketGem2 = 0,
        int SocketGem3 = 0)
    {
        public bool IsOccupied => ItemId >= 1;
    }

    public readonly record struct PurchaseResult(PurchaseOutcome Outcome, ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == PurchaseOutcome.Success;
    }
}

public static class ItemStackValueExtensions
{
    public static int Value(this ItemStack stack)
    {
        return ItemValueCodec.Encode(stack.Enchant, stack.Combine, stack.Refine, stack.Socket);
    }
}
