using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class ContainerMatrix
{
    public enum MoveOutcome
    {
        Success,
        NoOp,
        SourceOutOfRange,
        DestinationOutOfRange,
        SourceEmpty,
        InsufficientQuantity,
        InvalidQuantity,

        DestinationOccupied
    }

    public const int InventoryPageSlotCount = 64;

    public const byte InventoryPage0 = 0;
    public const byte InventoryPage1 = 1;
    public const byte Equipment = 2;
    public const byte StorePage0 = 3;
    public const byte StorePage1 = 4;

    private static readonly HashSet<int> KnownSorts =
    [
        201, 202, 203, 204, 205, 206, 207, 233, 235, 236, 237, 239,

        208, 3000, 209, 210, 211, 253, 212, 252, 213, 214, 215, 216,
        218, 219, 220, 221, 222, 223, 250, 224, 248, 225, 226, 227,
        228, 251, 229, 249, 230, 231, 232,
        240, 241, 242, 243, 244, 245, 246, 247,
        254, 255, 256,

        501, 502, 503, 504, 505, 523, 333, 506, 507, 508, 509, 510,
        511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522,
        524, 525, 526, 527, 528,

        598, 599, 600, 601, 602, 603,

        700, 701
    ];

    private static readonly HashSet<int> ImplementedContainerMoveSorts = [208, 210, 213];

    public static bool IsKnownSort(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    public static bool IsImplementedContainerMoveSort(int sort)
    {
        return ImplementedContainerMoveSorts.Contains(sort);
    }

    public static bool IsStackableSort(byte itemSort)
    {
        return ItemQuantityPolicy.IsStackableSort(itemSort);
    }

    public static bool IsSocketableItem(byte itemSort, byte itemType)
    {
        return itemType >= 3 && (itemSort is >= 7 and <= 21 || itemSort is 29);
    }

    public static bool TryGetMaxSlot(byte container, out int maxSlotInclusive)
    {
        switch (container)
        {
            case InventoryPage0:
            case InventoryPage1:
                maxSlotInclusive = 63;
                return true;
            case Equipment:
                maxSlotInclusive = EquipmentSlots.Count - 1;
                return true;
            case StorePage0:
            case StorePage1:
                maxSlotInclusive = 27;
                return true;
            default:
                maxSlotInclusive = 0;
                return false;
        }
    }

    public static bool IsValidSlot(byte container, int slot)
    {
        return slot >= 0 && TryGetMaxSlot(container, out var max) && slot <= max;
    }

    public static bool TryResolveContainers(int sort, int page1, int page2, out byte fromContainer,
        out byte toContainer)
    {
        switch (sort)
        {
            case 208 when IsInventoryPage(page1) && IsInventoryPage(page2):
                fromContainer = (byte)page1;
                toContainer = (byte)page2;
                return true;

            case 210 when IsInventoryPage(page1):
                fromContainer = (byte)page1;
                toContainer = Equipment;
                return true;

            case 213 when IsInventoryPage(page2):
                fromContainer = Equipment;
                toContainer = (byte)page2;
                return true;

            default:
                fromContainer = 0;
                toContainer = 0;
                return false;
        }
    }

    private static bool IsInventoryPage(int page)
    {
        return page is InventoryPage0 or InventoryPage1;
    }

    public static MoveOutcomeResult ResolveMove(
        byte fromContainer, int fromSlot, int requestedQuantity,
        byte toContainer, int toSlot, byte toX, byte toY,
        ItemStack? source, ItemStack? destination,
        byte sourceItemSort)
    {
        if (!IsValidSlot(fromContainer, fromSlot))
            return new MoveOutcomeResult(MoveOutcome.SourceOutOfRange, source, destination);

        if (!IsValidSlot(toContainer, toSlot))
            return new MoveOutcomeResult(MoveOutcome.DestinationOutOfRange, source, destination);

        if (source is not { } src)
            return new MoveOutcomeResult(MoveOutcome.SourceEmpty, source, destination);

        var sourceIsStackable = IsStackableSort(sourceItemSort);
        var quantity = ResolveMovedQuantity(src, requestedQuantity, sourceItemSort, sourceIsStackable);
        if (quantity is null)
            return new MoveOutcomeResult(MoveOutcome.InvalidQuantity, source, destination);

        var movedQuantity = quantity.Value;

        if (fromContainer == toContainer && fromSlot == toSlot)
            return new MoveOutcomeResult(MoveOutcome.NoOp, source, destination);

        if (sourceIsStackable && movedQuantity > src.Quantity)
            return new MoveOutcomeResult(MoveOutcome.InsufficientQuantity, source, destination);

        if (destination is not { } dst)
        {
            var moved = toContainer is InventoryPage0 or InventoryPage1
                ? src with { Quantity = movedQuantity, XPos = toX, YPos = toY }
                : src with { Quantity = movedQuantity };
            var remaining = src.Quantity - movedQuantity;
            ItemStack? newSource = remaining > 0 ? src with { Quantity = remaining } : null;
            return new MoveOutcomeResult(MoveOutcome.Success, newSource, moved);
        }

        if (!sourceIsStackable || dst.ItemId != src.ItemId ||
            dst.Quantity is < ItemQuantityPolicy.MinStackQuantity or > ItemQuantityPolicy.MaxStackQuantity ||
            dst.Quantity + movedQuantity > ItemQuantityPolicy.MaxStackQuantity)
            return new MoveOutcomeResult(MoveOutcome.DestinationOccupied, source, destination);

        var merged = toContainer is InventoryPage0 or InventoryPage1
            ? dst with { Quantity = dst.Quantity + movedQuantity, XPos = toX, YPos = toY }
            : dst with { Quantity = dst.Quantity + movedQuantity };
        var remainingAfterMerge = src.Quantity - movedQuantity;
        ItemStack? newSourceAfterMerge = remainingAfterMerge > 0 ? src with { Quantity = remainingAfterMerge } : null;
        return new MoveOutcomeResult(MoveOutcome.Success, newSourceAfterMerge, merged);
    }

    private static int? ResolveMovedQuantity(ItemStack source, int requestedQuantity, byte sourceItemSort,
        bool sourceIsStackable)
    {
        if (requestedQuantity <= 0)
            return null;

        if (sourceIsStackable)
            return source.Quantity is >= ItemQuantityPolicy.MinStackQuantity
                       and <= ItemQuantityPolicy.MaxStackQuantity &&
                   requestedQuantity <= ItemQuantityPolicy.MaxStackQuantity
                ? requestedQuantity
                : null;

        if (requestedQuantity != 1)
            return null;

        return sourceItemSort switch
        {
            ItemQuantityPolicy.PetSort when source.Quantity is >= ItemQuantityPolicy.MinStackQuantity and <=
                ItemQuantityPolicy.MaxPetActivity => source.Quantity,
            _ when ItemQuantityPolicy.CarriesNoQuantity(sourceItemSort) && source.Quantity is 0 or 1 =>
                source.Quantity,
            _ => null
        };
    }

    public static ProjectedContainers ApplyMove(
        MoveOutcomeResult move,
        byte fromContainer, int fromSlot, ImmutableDictionary<byte, ItemStack> fromCurrent,
        byte toContainer, int toSlot, ImmutableDictionary<byte, ItemStack> toCurrent)
    {
        if (fromContainer == toContainer)
        {
            if (fromSlot == toSlot)
                return new ProjectedContainers(fromCurrent, fromCurrent);

            var updated = ApplySlotChange(fromCurrent, (byte)fromSlot, move.NewSource);
            updated = ApplySlotChange(updated, (byte)toSlot, move.NewDestination);
            return new ProjectedContainers(updated, updated);
        }

        var newFrom = ApplySlotChange(fromCurrent, (byte)fromSlot, move.NewSource);
        var newTo = ApplySlotChange(toCurrent, (byte)toSlot, move.NewDestination);
        return new ProjectedContainers(newFrom, newTo);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? newValue)
    {
        return newValue is { } value ? current.SetItem(slot, value) : current.Remove(slot);
    }

    public readonly record struct MoveOutcomeResult(
        MoveOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination)
    {
        public bool Succeeded => Outcome is MoveOutcome.Success or MoveOutcome.NoOp;
    }

    public readonly record struct ProjectedContainers(
        ImmutableDictionary<byte, ItemStack> From,
        ImmutableDictionary<byte, ItemStack> To);
}
