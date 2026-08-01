using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Npcs;

public static class WarPointShopPolicy
{
    public enum BuyOutcome
    {
        NotWarPointItem,

        Proceed,

        PriceUnavailable,

        WrongNpcForItem,

        DestinationConflict,

        InvalidQuantity,

        InsufficientContributionPoints
    }

    private const int FixedStateStampRangeStart = 86700;

    private const int FixedStateStampRangeEndInclusive = 86725;

    private const byte FixedStateStampEnchant = 40;

    private const byte FixedStateStampCombine = 12;

    public static bool IsFixedStateStampItem(int itemId)
    {
        return itemId is >= FixedStateStampRangeStart and <= FixedStateStampRangeEndInclusive;
    }

    public static BuyResolution ResolveBuy(WarPointShopCatalog catalog, int npcId, ItemDefinition itemDefinition,
        int requestedQuantity, ItemStack? destinationSlot, int playerContributionPoints)
    {
        var item = itemDefinition.Item;

        if (!WarPointShopCatalog.IsWarPointNpc(npcId))
            return new BuyResolution(BuyOutcome.NotWarPointItem, 0, 0, null);

        if (!catalog.TryGetPrice(item.ItemId, out var price))
            return new BuyResolution(BuyOutcome.PriceUnavailable, 0, 0, null);

        if (!price.DisplaysAtNpc(npcId))
            return new BuyResolution(BuyOutcome.WrongNpcForItem, 0, 0, null);

        if (ContainerMatrix.IsStackableSort(item.Sort))
        {
            var quantity = requestedQuantity < 1 ? 1 : requestedQuantity;

            if (quantity > GroundItemPickupPolicy.MaxStackQuantity)
                return new BuyResolution(BuyOutcome.InvalidQuantity, 0, 0, null);

            if (destinationSlot is { } existing)
            {
                if (existing.ItemId != item.ItemId)
                    return new BuyResolution(BuyOutcome.DestinationConflict, 0, 0, null);

                var merged = existing.Quantity + quantity;
                if (merged > GroundItemPickupPolicy.MaxStackQuantity)
                    return new BuyResolution(BuyOutcome.DestinationConflict, 0, 0, null);

                return Cost(price, quantity, playerContributionPoints, existing with { Quantity = merged });
            }

            return Cost(price, quantity, playerContributionPoints,
                new ItemStack(item.ItemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        if (destinationSlot is not null)
            return new BuyResolution(BuyOutcome.DestinationConflict, 0, 0, null);

        var grantedStack = IsFixedStateStampItem(item.ItemId)
            ? new ItemStack(item.ItemId, 1, FixedStateStampEnchant, FixedStateStampCombine, 0, 0, 0, 0, 0, 0, 0)
            : new ItemStack(item.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        return Cost(price, requestedQuantity, playerContributionPoints, grantedStack);
    }

    private static BuyResolution Cost(WarPointPriceEntry price, int quantity, int playerContributionPoints,
        ItemStack newDestinationStack)
    {
        var effectiveQuantity = quantity > 0 ? quantity : 1;
        var warPointCost = price.WarPointPrice * effectiveQuantity;
        var contributionPointCost = price.ContributionPointPrice * effectiveQuantity;

        if (contributionPointCost > 0 && playerContributionPoints < contributionPointCost)
            return new BuyResolution(BuyOutcome.InsufficientContributionPoints, 0, 0, null);

        return new BuyResolution(BuyOutcome.Proceed, warPointCost, contributionPointCost, newDestinationStack);
    }

    public readonly record struct BuyResolution(
        BuyOutcome Outcome,
        int WarPointCost,
        int ContributionPointCost,
        ItemStack? NewDestinationStack)
    {
        public bool ShouldProceed => Outcome == BuyOutcome.Proceed;
    }
}
