using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Game.Domain.World.Npcs;

/// <summary>
///     Pure, Zone-independent policy for NPC-shop buy/sell (<c>ProcessForInventoryToNPCShop</c>/
///     <c>ProcessForNPCShopToInventory</c>, <c>Server/ts25zone/S04_MyWork05.cpp:1398/1716</c>). Money-balance
///     sufficiency and the upper money cap are deliberately not checked here -- both are enforced atomically
///     by the SQL layer, and the legacy itself Quit()s (disconnects) on either condition for this action pair,
///     so letting the SQL guard's exception propagate to an Abort reproduces that without this policy needing
///     to know the player's current balance. Contribution-Point sufficiency, by contrast, IS checked here --
///     unlike Money, <see cref="PlayerRuntimeState.ContributionPoints" /> already
///     lives in-memory, the same posture <c>CraftLegendaryPetHandler</c>/<c>TribeActionHandler</c> use.
/// </summary>
/// <remarks>
///     The buy-side WarPoint-shop branch (<c>USE_WAR_POINT_SYSTEM</c>) is now modeled separately by
///     <see cref="WarPointShopPolicy" />/<c>WarPointShopService</c> (a WarPoint item bypasses the ordinary
///     <c>iCheckNPCShop == 2</c> / rent gates this policy enforces); the ordinary silver + Contribution-Point
///     cost (<c>BuyCost2</c>) that every non-WarPoint NPC shop charges is still reproduced here. The sell-side
///     WarPoint exemption -- the <c>99703</c>-<c>99756</c> range that skips the rare/costume sell-block
///     (<c>Server/ts25zone/S04_MyWork05.cpp:1496-1522</c>, <c>#elif defined LNW33</c> branch) -- IS handled
///     here, see <see cref="IsSellExempt" />.
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
        ///     <c>wAvatar.aKillOtherTribe &lt; tCostCP</c> -- not enough Contribution Points for this item's
        ///     <c>iBuyCost2</c>. Quit()-worthy, the same treatment the legacy gives an insufficient Money balance.
        /// </summary>
        InsufficientContributionPoints,

        /// <summary><c>IsRentItem</c> -- rentable items can't be bought through this action; clean failure (<c>*tResult=1</c>).</summary>
        RentItemNotPurchasable,

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

    /// <summary>
    ///     <c>IsRentItem</c> (function.h:2216) -- a contiguous rentable-item ID range gated on both buy and sell.
    ///     Not stored in <c>world.Items</c> (the legacy itself hard-codes it too), so it's kept here as a small
    ///     closed range rather than a schema column.
    /// </summary>
    private const int RentItemIdStart = 76500;

    private const int RentItemIdEndInclusive = 76540;

    /// <summary>
    ///     NPC-sell exemption range (<c>Server/ts25zone/S04_MyWork05.cpp:1496-1522</c>): a WarPoint-adjacent gear
    ///     range that is allowed to skip the normal rare-item and costume sell-block checks and be sold to the
    ///     NPC for its sell price. The <c>#elif defined LNW33</c> branch (<c>99703</c>-<c>99756</c>) is the one
    ///     that actually compiles in this zone translation unit; the alternate <c>74200</c>-<c>74223</c> branch
    ///     is gated by <c>#ifdef USE_CUSTOME_CREATE</c>, which is force-defined only in the separate
    ///     <c>ts25login/S04_MyWork02.cpp:1</c> and is dead here (its <c>DEFINE.h:37</c> definition sits in the
    ///     dead <c>#else</c> of <c>#ifdef M33</c>) -- so the <c>74200</c>-<c>74223</c> range is deliberately NOT
    ///     carried. The exemption is scoped to the rare/costume blocks only; it does not override the earlier
    ///     <c>CheckNpcSell == 1</c> or rent-item rejections.
    /// </summary>
    private const int SellExemptRangeStart = 99703;

    private const int SellExemptRangeEndInclusive = 99756;

    /// <summary><c>IsValidTownAll</c> (mapcheck.h:116-128) -- both buy and sell require the CURRENT zone to be one of these.</summary>
    public static readonly IReadOnlySet<short> TownZoneNumbers = new HashSet<short> { 1, 6, 11, 37, 140 };

    /// <summary>
    ///     <c>IsValidCostume</c> (function.h:1798-2008) -- costume-slot item IDs an NPC shop refuses to buy back on
    ///     sell, gated independently of <see cref="RareItemType" />. Transcribed range-by-range from the legacy
    ///     switch, including its own dead/commented-out gaps (93331-93333, 93382-93384) -- same "hand-curated,
    ///     not in world.Items" posture as <see cref="Enchant.CapeUpgradeResolver" />'s item lists.
    /// </summary>
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

    /// <summary>See <see cref="SellExemptRangeStart" /> -- the live (LNW33) NPC-sell rare/costume-block exemption.</summary>
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

        // WarPoint-adjacent sell exemption (S04_MyWork05.cpp:1496-1522, live LNW33 branch): items in
        // 99703-99756 jump past the rare/costume sell-block straight to the sell for their sell price. Placed
        // after the CheckNpcSell==1 / rent-item gates above (which the exemption does not override) and before
        // the rare/costume blocks below (which it does skip).
        if (IsSellExempt(item.ItemId))
            return new SellResult(SellOutcome.Success, item.SellCost, null);

        // iType >= IRARE && iValue != 0 (S04_MyWork05.cpp:1508-1513): Fenrir never reassembles the packed
        // iValue int -- "any of its 4 decomposed bytes nonzero" is the closest faithful reading here.
        if (item.Type >= RareItemType &&
            (sourceStack.Enchant != 0 || sourceStack.Combine != 0 || sourceStack.Refine != 0 ||
             sourceStack.Socket != 0))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        if (IsCostumeItem(item.ItemId))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        return new SellResult(SellOutcome.Success, item.SellCost, null);
    }

    /// <summary>
    ///     Ports <c>ProcessForNPCShopToInventory</c>'s dispatch (WarPoint-shop branch excluded, see class
    ///     remarks). <paramref name="requestedQuantity" /> is only meaningful for a stackable item.
    /// </summary>
    /// <param name="playerContributionPoints">
    ///     <see cref="PlayerRuntimeState.ContributionPoints" /> at the moment of the
    ///     request -- only consulted when the item's <c>BuyCost2</c> is nonzero.
    /// </param>
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

    /// <summary>
    ///     <c>CheckBuyCostFree</c>: computes both the silver cost (<see cref="ResolveBuyCost" />) and the
    ///     Contribution-Point cost (<c>iBuyCost2</c>, quantity-scaled for a stackable item, never discounted --
    ///     the zone-291 10% break only ever applies to silver) in one place, and gates on CP sufficiency the same
    ///     way the legacy <c>Quit()</c>s when <c>wAvatar.aKillOtherTribe &lt; tCostCP</c>.
    /// </summary>
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

    /// <summary>
    ///     <c>CheckBuyCostFree</c>: a 10% discount applies only on zone 291, which isn't one of the town zones
    ///     this action requires -- dead/unreachable in this call path, kept for source fidelity.
    /// </summary>
    private static int ResolveBuyCost(ItemRowDto item, int quantity, short currentZoneNumber)
    {
        var unitCost = currentZoneNumber == 291 ? (int)(item.BuyCost * 0.9f) : item.BuyCost;
        return ContainerMatrix.IsStackableSort(item.Sort) ? unitCost * quantity : unitCost;
    }

    /// <summary>
    ///     On <see cref="Succeeded" />, the caller (<c>GenericActionService.SellToNpcShopAsync</c>) logs a
    ///     game.EventLog audit row (Category=NpcShopTrade) once the money credit + container replace have
    ///     durably persisted -- legacy <c>GL_621_NSHOP_ITEM</c>, see <see cref="EventLogCategory.NpcShopTrade" />'s
    ///     own remarks. This pure policy performs no logging itself (no I/O in <c>Fenrir.Application.Game.Domain</c>).
    /// </summary>
    public readonly record struct SellResult(
        SellOutcome Outcome,
        long MoneyGained,
        ItemStack? RemainingSourceStack)
    {
        public bool Succeeded => Outcome == SellOutcome.Success;
    }

    /// <summary>
    ///     On <see cref="Succeeded" />, the caller (<c>GenericActionService.BuyFromNpcShopAsync</c>) logs a
    ///     game.EventLog audit row (Category=NpcShopTrade) once the money debit + container replace have
    ///     durably persisted -- legacy <c>GL_621_NSHOP_ITEM</c>, see <see cref="EventLogCategory.NpcShopTrade" />'s
    ///     own remarks. This pure policy performs no logging itself (no I/O in <c>Fenrir.Application.Game.Domain</c>).
    /// </summary>
    public readonly record struct BuyResult(
        BuyOutcome Outcome,
        int MoneyCost,
        int CpCost,
        ItemStack? NewDestinationStack)
    {
        public bool Succeeded => Outcome == BuyOutcome.Success;

        /// <summary>Clean <c>*tResult=1</c> failures (NOT disconnect-worthy) per the verified source.</summary>
        public bool IsCleanFailure => Outcome is BuyOutcome.NotSellableHere or BuyOutcome.InvalidQuantity
            or BuyOutcome.RentItemNotPurchasable;
    }
}
