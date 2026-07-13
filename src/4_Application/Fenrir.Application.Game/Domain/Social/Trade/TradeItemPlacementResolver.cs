using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeItemPlacementResolver
{
    public enum PlacementOutcome
    {
        Success,

        SourceEmpty,

        UnknownItem,

        UntradeableItem,

        QuantityOutOfRange,

        InsufficientSourceQuantity,

        DestinationIncompatible
    }

    public const int MinQuantity = 1;

    public const int MaxQuantity = 999;

    public static bool IsValidTradeSlot(int slot)
    {
        return slot >= 0 && slot < TradeLimits.SlotCount;
    }

    public static bool IsValidInventoryPage(int page)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1;
    }

    public static bool IsValidInventorySlot(int slot)
    {
        return slot is >= 0 and <= 63;
    }

    public static bool IsValidGridCoordinate(int coordinate)
    {
        return coordinate is >= 0 and <= 7;
    }

    public static bool IsPremiumPageAccessAllowed(long premiumExpireUnixSeconds, long nowUnixSeconds)
    {
        return premiumExpireUnixSeconds >= nowUnixSeconds;
    }

    public static PlacementResult ResolveDeposit(
        ItemStack? source,
        int requestedQuantity,
        ItemStack? destination,
        ItemDefinition? sourceItemDefinition,
        bool sourceIsSocketEligible)
    {
        if (source is not { } src)
            return new PlacementResult(PlacementOutcome.SourceEmpty, source, destination);

        if (sourceItemDefinition is null)
            return new PlacementResult(PlacementOutcome.UnknownItem, source, destination);

        if (sourceItemDefinition.Item.CheckAvatarTrade == 1)
            return new PlacementResult(PlacementOutcome.UntradeableItem, source, destination);

        return ResolveTransfer(src, requestedQuantity, destination, sourceItemDefinition, sourceIsSocketEligible);
    }

    public static PlacementResult ResolveWithdrawal(
        ItemStack? source,
        int requestedQuantity,
        ItemStack? destination,
        ItemDefinition? sourceItemDefinition,
        bool sourceIsSocketEligible)
    {
        if (source is not { } src)
            return new PlacementResult(PlacementOutcome.SourceEmpty, source, destination);

        if (sourceItemDefinition is null)
            return new PlacementResult(PlacementOutcome.UnknownItem, source, destination);

        return ResolveTransfer(src, requestedQuantity, destination, sourceItemDefinition, sourceIsSocketEligible);
    }

    public static PlacementResult ResolveRearrange(
        ItemStack? source,
        int requestedQuantity,
        ItemStack? destination,
        ItemDefinition? sourceItemDefinition,
        bool sourceIsSocketEligible)
    {
        if (source is not { } src)
            return new PlacementResult(PlacementOutcome.SourceEmpty, source, destination);

        if (sourceItemDefinition is null)
            return new PlacementResult(PlacementOutcome.UnknownItem, source, destination);

        return ResolveTransfer(src, requestedQuantity, destination, sourceItemDefinition, sourceIsSocketEligible);
    }

    private static PlacementResult ResolveTransfer(
        ItemStack source,
        int requestedQuantity,
        ItemStack? destination,
        ItemDefinition sourceItemDefinition,
        bool sourceIsSocketEligible)
    {
        return ContainerMatrix.IsStackableSort(sourceItemDefinition.Item.Sort)
            ? ResolveStackableTransfer(source, requestedQuantity, destination, sourceIsSocketEligible)
            : ResolveNonStackableTransfer(source, destination, sourceIsSocketEligible);
    }

    private static PlacementResult ResolveStackableTransfer(
        ItemStack source, int requestedQuantity, ItemStack? destination, bool sourceIsSocketEligible)
    {
        if (requestedQuantity is < MinQuantity or > MaxQuantity)
            return new PlacementResult(PlacementOutcome.QuantityOutOfRange, source, destination);

        if (requestedQuantity > source.Quantity)
            return new PlacementResult(PlacementOutcome.InsufficientSourceQuantity, source, destination);

        if (destination is { } occupant && occupant.ItemId != source.ItemId)
            return new PlacementResult(PlacementOutcome.DestinationIncompatible, source, destination);

        var mergedQuantity = (destination?.Quantity ?? 0) + requestedQuantity;
        if (mergedQuantity > MaxQuantity)
            return new PlacementResult(PlacementOutcome.DestinationIncompatible, source, destination);

        var remainingSourceQuantity = source.Quantity - requestedQuantity;
        var fullyEmptied = remainingSourceQuantity <= 0;

        var (gem1, gem2, gem3) = fullyEmptied
            ? sourceIsSocketEligible
                ? (source.SocketGem1, source.SocketGem2, source.SocketGem3)
                : (0, 0, 0)
            : (destination?.SocketGem1 ?? 0, destination?.SocketGem2 ?? 0, destination?.SocketGem3 ?? 0);

        var newDestination = new ItemStack(
            source.ItemId, mergedQuantity,
            0, 0, 0, 0,
            gem1, gem2, gem3,
            destination?.ExpireDate ?? 0,
            0);

        ItemStack? newSource = fullyEmptied ? null : source with { Quantity = remainingSourceQuantity };

        return new PlacementResult(PlacementOutcome.Success, newSource, newDestination);
    }

    private static PlacementResult ResolveNonStackableTransfer(
        ItemStack source, ItemStack? destination, bool sourceIsSocketEligible)
    {
        if (destination is not null)
            return new PlacementResult(PlacementOutcome.DestinationIncompatible, source, destination);

        var (gem1, gem2, gem3) = sourceIsSocketEligible
            ? (source.SocketGem1, source.SocketGem2, source.SocketGem3)
            : (0, 0, 0);

        var newDestination = source with { SocketGem1 = gem1, SocketGem2 = gem2, SocketGem3 = gem3 };

        return new PlacementResult(PlacementOutcome.Success, null, newDestination);
    }

    public readonly record struct PlacementResult(
        PlacementOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination)
    {
        public bool Succeeded => Outcome == PlacementOutcome.Success;
    }
}
