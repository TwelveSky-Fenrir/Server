using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Npcs;

public static class WarPointShopPolicy
{
    public enum BuyOutcome
    {

                NotWarPointItem,

                Proceed,

                WrongNpc,

                DestinationConflict,

                InsufficientContributionPoints
    }

        private const int FixedStateStampRangeStart = 86700;

        private const int FixedStateStampRangeEndInclusive = 86725;

        public static bool IsFixedStateStampItem(int itemId)
    {
        return itemId is >= FixedStateStampRangeStart and <= FixedStateStampRangeEndInclusive;
    }

        public static BuyResolution ResolveBuy(WarPointShopCatalog catalog, int npcId, ItemDefinition itemDefinition,
        int requestedQuantity, ItemStack? destinationSlot, int playerContributionPoints)
    {
        var item = itemDefinition.Item;

        if (!WarPointShopCatalog.IsWarPointNpc(npcId) || !catalog.TryGetPrice(item.ItemId, out var price))
            return new BuyResolution(BuyOutcome.NotWarPointItem, 0, 0, null);

        if (!price.DisplaysAtNpc(npcId))
            return new BuyResolution(BuyOutcome.WrongNpc, 0, 0, null);

        if (ContainerMatrix.IsStackableSort(item.Sort))
        {
            var quantity = requestedQuantity < 1 ? 1 : requestedQuantity;

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

        return Cost(price, requestedQuantity, playerContributionPoints,
            new ItemStack(item.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
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
