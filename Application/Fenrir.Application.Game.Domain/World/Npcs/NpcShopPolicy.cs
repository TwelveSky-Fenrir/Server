using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Game.Domain.World.Npcs;

public static class NpcShopPolicy
{
    public enum BuyOutcome
    {
        Success,

                NotSellableHere,

                NotInCatalog,

                InvalidQuantity,

                DestinationConflict,

                InsufficientContributionPoints,

                RentItemNotPurchasable,

                BelowMinimumLevel
    }

    public enum SellOutcome
    {
        Success,

                Rejected,

                InvalidQuantity
    }

        private const byte RareItemType = 3;

        public const byte SpecialShopNpcType = 13;

    public const short SpecialShopMinimumLevel = 113;

        private const int RentItemIdStart = 76500;

    private const int RentItemIdEndInclusive = 76540;

        private const int SellExemptRangeStart = 99703;

    private const int SellExemptRangeEndInclusive = 99756;

        public static readonly IReadOnlySet<short> TownZoneNumbers = new HashSet<short> { 1, 6, 11, 37, 140 };

        private static readonly (int Start, int EndInclusive)[] CostumeItemIdRanges =
    [
        (301, 402),
        (1801, 1803),
        (1891, 1893),
        (2146, 2148),
        (17701, 17703),
        (18124, 18132),
        (76524, 76526),
        (93301, 93330),
        (93334, 93345),
        (93376, 93381),
        (93385, 93405)
    ];

    private static bool IsRentItem(int itemId)
    {
        return itemId is >= RentItemIdStart and <= RentItemIdEndInclusive;
    }

        public static bool IsSellExempt(int itemId)
    {
        return itemId is >= SellExemptRangeStart and <= SellExemptRangeEndInclusive;
    }

    private static bool IsCostumeItem(int itemId)
    {
        foreach (var range in CostumeItemIdRanges)
            if (itemId >= range.Start && itemId <= range.EndInclusive)
                return true;

        return false;
    }

        public static SellResult ResolveSell(ItemDefinition itemDefinition, ItemStack sourceStack, int requestedQuantity)
    {
        var item = itemDefinition.Item;

        if (item.CheckNpcSell == 1)
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        if (IsRentItem(item.ItemId))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        var isStackable = ContainerMatrix.IsStackableSort(item.Sort);

        if (isStackable)
        {
            if (requestedQuantity < 1 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity ||
                requestedQuantity > sourceStack.Quantity)
                return new SellResult(SellOutcome.InvalidQuantity, 0, sourceStack);

            var gained = (long)item.SellCost * requestedQuantity;
            var remainingQuantity = sourceStack.Quantity - requestedQuantity;
            var remaining = remainingQuantity > 0
                ? sourceStack with { Quantity = remainingQuantity }
                : (ItemStack?)null;
            return new SellResult(SellOutcome.Success, gained, remaining);
        }

        if (IsSellExempt(item.ItemId))
            return new SellResult(SellOutcome.Success, item.SellCost, null);

        if (item.Type >= RareItemType &&
            (sourceStack.Enchant != 0 || sourceStack.Combine != 0 || sourceStack.Refine != 0 ||
             sourceStack.Socket != 0))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        if (IsCostumeItem(item.ItemId))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        return new SellResult(SellOutcome.Success, item.SellCost, null);
    }

        public static BuyResult ResolveBuy(NpcDefinition npc, ItemDefinition itemDefinition, int requestedQuantity,
        ItemStack? destinationSlot, short playerLevel, short currentZoneNumber, int playerContributionPoints)
    {
        var item = itemDefinition.Item;

        var inCatalog = false;
        foreach (var shopItem in npc.ShopItems)
            if (shopItem.ItemId == item.ItemId)
            {
                inCatalog = true;
                break;
            }

        if (!inCatalog)
            return new BuyResult(BuyOutcome.NotInCatalog, 0, 0, null);

        if (npc.Npc.Type == SpecialShopNpcType && playerLevel < SpecialShopMinimumLevel)
            return new BuyResult(BuyOutcome.BelowMinimumLevel, 0, 0, null);

        if (item.CheckNpcShop != 2)
            return new BuyResult(BuyOutcome.NotSellableHere, 0, 0, null);

        if (IsRentItem(item.ItemId))
            return new BuyResult(BuyOutcome.RentItemNotPurchasable, 0, 0, null);

        var isStackable = ContainerMatrix.IsStackableSort(item.Sort);

        if (isStackable)
        {
            if (requestedQuantity < 1 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                return new BuyResult(BuyOutcome.InvalidQuantity, 0, 0, null);

            if (destinationSlot is { } existing)
            {
                if (existing.ItemId != item.ItemId)
                    return new BuyResult(BuyOutcome.DestinationConflict, 0, 0, null);

                var mergedQuantity = existing.Quantity + requestedQuantity;
                if (mergedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                    return new BuyResult(BuyOutcome.DestinationConflict, 0, 0, null);

                if (!TryResolveCost(item, requestedQuantity, currentZoneNumber, playerContributionPoints,
                        out var moneyCost, out var cpCost, out var costFailure))
                    return new BuyResult(costFailure, 0, 0, null);

                return new BuyResult(BuyOutcome.Success, moneyCost, cpCost,
                    existing with { Quantity = mergedQuantity });
            }

            if (!TryResolveCost(item, requestedQuantity, currentZoneNumber, playerContributionPoints,
                    out var newMoneyCost, out var newCpCost, out var newCostFailure))
                return new BuyResult(newCostFailure, 0, 0, null);

            var newStack = new ItemStack(item.ItemId, requestedQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return new BuyResult(BuyOutcome.Success, newMoneyCost, newCpCost, newStack);
        }

        if (destinationSlot is not null)
            return new BuyResult(BuyOutcome.DestinationConflict, 0, 0, null);

        if (!TryResolveCost(item, 1, currentZoneNumber, playerContributionPoints, out var singleMoneyCost,
                out var singleCpCost, out var singleCostFailure))
            return new BuyResult(singleCostFailure, 0, 0, null);

        return new BuyResult(BuyOutcome.Success, singleMoneyCost, singleCpCost,
            new ItemStack(item.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

        private static bool TryResolveCost(ItemRowDto item, int quantity, short currentZoneNumber,
        int playerContributionPoints, out int moneyCost, out int cpCost, out BuyOutcome failureOutcome)
    {
        moneyCost = ResolveBuyCost(item, quantity, currentZoneNumber);
        cpCost = ContainerMatrix.IsStackableSort(item.Sort) ? item.BuyCost2 * quantity : item.BuyCost2;

        if (cpCost > 0 && playerContributionPoints < cpCost)
        {
            failureOutcome = BuyOutcome.InsufficientContributionPoints;
            return false;
        }

        failureOutcome = default;
        return true;
    }

        private static int ResolveBuyCost(ItemRowDto item, int quantity, short currentZoneNumber)
    {
        var unitCost = currentZoneNumber == 291 ? (int)(item.BuyCost * 0.9f) : item.BuyCost;
        return ContainerMatrix.IsStackableSort(item.Sort) ? unitCost * quantity : unitCost;
    }

        public readonly record struct SellResult(
        SellOutcome Outcome,
        long MoneyGained,
        ItemStack? RemainingSourceStack)
    {
        public bool Succeeded => Outcome == SellOutcome.Success;
    }

        public readonly record struct BuyResult(
        BuyOutcome Outcome,
        int MoneyCost,
        int CpCost,
        ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == BuyOutcome.Success;

                public bool IsCleanFailure => Outcome is BuyOutcome.NotSellableHere or BuyOutcome.InvalidQuantity
            or BuyOutcome.RentItemNotPurchasable;
    }
}
