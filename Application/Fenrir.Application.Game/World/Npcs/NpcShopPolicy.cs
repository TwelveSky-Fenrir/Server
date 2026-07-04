using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Npcs;

/// <summary>
///     Pure, Zone-independent policy for NPC-shop buy/sell (<c>ProcessForInventoryToNPCShop</c>/
///     <c>ProcessForNPCShopToInventory</c>, <c>Server/ts25zone/S04_MyWork05.cpp:1398/1716</c>). Money-balance
///     sufficiency and the upper money cap are deliberately not checked here -- both are enforced atomically
///     by the SQL layer, and the legacy itself Quit()s (disconnects) on either condition for this action pair,
///     so letting the SQL guard's exception propagate to an Abort reproduces that without this policy needing
///     to know the player's current balance.
/// </summary>
/// <remarks>
///     Not modeled: (1) <c>IsRentItem</c>'s rentable-item exclusion (no equivalent in Fenrir's
///     <c>world.Items</c> schema); (2) the WarPoint-shop branch and Contribution-Point cost (an item with
///     <c>BuyCost2 &gt; 0</c> is rejected as a clean failure instead); (3) <c>IsValidCostume</c> exclusion on sell.
/// </remarks>
public static class NpcShopPolicy
{
    public enum BuyOutcome
    {
        Success,

        /// <summary><c>iCheckNPCShop != 2</c> -- clean failure (<c>*tResult=1</c>, NOT a disconnect).</summary>
        NotSellableHere,

        /// <summary>Not present in ANY of this NPC's shop pages/slots -- structural cheat (Quit()-worthy).</summary>
        NotInCatalog,

        /// <summary>Stackable quantity outside [1, <see cref="GroundItemPickupPolicy.MaxStackQuantity" />] -- clean failure.</summary>
        InvalidQuantity,

        /// <summary>
        ///     Destination occupied by a DIFFERENT item, or a merge would exceed the stack cap, or (non-stackable)
        ///     destination is occupied at all -- Quit()-worthy.
        /// </summary>
        DestinationConflict,

        /// <summary>
        ///     This item costs Contribution Points (<c>iBuyCost2 &gt; 0</c>) -- NOT supported (see class remarks); clean
        ///     failure.
        /// </summary>
        ContributionCostUnsupported,

        /// <summary><c>nType == 13</c> shop, player below <see cref="SpecialShopMinimumLevel" /> -- Quit()-worthy.</summary>
        BelowMinimumLevel
    }

    public enum SellOutcome
    {
        Success,

        /// <summary>
        ///     <c>iCheckNPCSell == 1</c>, or a Rare/Elite item with any upgrade value applied -- clean failure, no graceful
        ///     path in the legacy (Quit()-worthy).
        /// </summary>
        Rejected,

        /// <summary>
        ///     Stackable quantity outside [1, <see cref="GroundItemPickupPolicy.MaxStackQuantity" />] or exceeding the held
        ///     quantity.
        /// </summary>
        InvalidQuantity
    }

    /// <summary>
    ///     <c>IRARE</c> (STRUCT.h:1657) -- items of this type or higher (IELITE=4) cannot be sold if
    ///     enchanted/combined/refined/socketed.
    /// </summary>
    private const byte RareItemType = 3;

    /// <summary><c>nType == 13</c> gate on buy (<c>ProcessForNPCShopToInventory</c>) -- requires <c>LV_M1</c> (DEFINE.h:451).</summary>
    public const byte SpecialShopNpcType = 13;

    public const short SpecialShopMinimumLevel = 113;

    /// <summary><c>IsValidTownAll</c> (mapcheck.h:116-128) -- both buy and sell require the CURRENT zone to be one of these.</summary>
    public static readonly IReadOnlySet<short> TownZoneNumbers = new HashSet<short> { 1, 6, 11, 37, 140 };

    /// <summary>
    ///     Ports <c>ProcessForInventoryToNPCShop</c>'s branch on <c>iSort</c> (stackable vs. not) exactly.
    ///     Every rejection in the legacy function is a <c>Quit()</c> -- treat any non-<see cref="SellOutcome.Success" />
    ///     result as disconnect-worthy, not a soft failure.
    /// </summary>
    public static SellResult ResolveSell(ItemDefinition itemDefinition, ItemStack sourceStack, int requestedQuantity)
    {
        var item = itemDefinition.Item;

        if (item.CheckNpcSell == 1)
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

        // iType >= IRARE && iValue != 0 (S04_MyWork05.cpp:1508-1513): Fenrir never reassembles the packed
        // iValue int -- "any of its 4 decomposed bytes nonzero" is the closest faithful reading here.
        if (item.Type >= RareItemType &&
            (sourceStack.Enchant != 0 || sourceStack.Combine != 0 || sourceStack.Refine != 0 ||
             sourceStack.Socket != 0))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        return new SellResult(SellOutcome.Success, item.SellCost, null);
    }

    /// <summary>
    ///     Ports <c>ProcessForNPCShopToInventory</c>'s dispatch (WarPoint-shop branch excluded, see class
    ///     remarks). <paramref name="requestedQuantity" /> is only meaningful for a stackable item.
    /// </summary>
    public static BuyResult ResolveBuy(NpcDefinition npc, ItemDefinition itemDefinition, int requestedQuantity,
        ItemStack? destinationSlot, short playerLevel, short currentZoneNumber)
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
            return new BuyResult(BuyOutcome.NotInCatalog, 0, null);

        if (npc.Npc.Type == SpecialShopNpcType && playerLevel < SpecialShopMinimumLevel)
            return new BuyResult(BuyOutcome.BelowMinimumLevel, 0, null);

        if (item.CheckNpcShop != 2)
            return new BuyResult(BuyOutcome.NotSellableHere, 0, null);

        if (item.BuyCost2 > 0)
            return new BuyResult(BuyOutcome.ContributionCostUnsupported, 0, null);

        var isStackable = ContainerMatrix.IsStackableSort(item.Sort);

        if (isStackable)
        {
            if (requestedQuantity < 1 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                return new BuyResult(BuyOutcome.InvalidQuantity, 0, null);

            if (destinationSlot is { } existing)
            {
                if (existing.ItemId != item.ItemId)
                    return new BuyResult(BuyOutcome.DestinationConflict, 0, null);

                var mergedQuantity = existing.Quantity + requestedQuantity;
                if (mergedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                    return new BuyResult(BuyOutcome.DestinationConflict, 0, null);

                var cost = ResolveBuyCost(item, requestedQuantity, currentZoneNumber);
                return new BuyResult(BuyOutcome.Success, cost, existing with { Quantity = mergedQuantity });
            }

            var newStack = new ItemStack(item.ItemId, requestedQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return new BuyResult(BuyOutcome.Success, ResolveBuyCost(item, requestedQuantity, currentZoneNumber),
                newStack);
        }

        if (destinationSlot is not null)
            return new BuyResult(BuyOutcome.DestinationConflict, 0, null);

        return new BuyResult(BuyOutcome.Success, ResolveBuyCost(item, 1, currentZoneNumber),
            new ItemStack(item.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>
    ///     <c>CheckBuyCostFree</c>: a 10% discount applies only on zone 291, which isn't one of the town zones
    ///     this action requires -- dead/unreachable in this call path, kept for source fidelity.
    /// </summary>
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
        ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == BuyOutcome.Success;

        /// <summary>Clean <c>*tResult=1</c> failures (NOT disconnect-worthy) per the verified source.</summary>
        public bool IsCleanFailure => Outcome is BuyOutcome.NotSellableHere or BuyOutcome.InvalidQuantity
            or BuyOutcome.ContributionCostUnsupported;
    }
}
