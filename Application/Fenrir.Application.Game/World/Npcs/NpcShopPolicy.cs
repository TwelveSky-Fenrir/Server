using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Npcs;

/// <summary>
///     Pure, Zone-independent policy for NPC-shop buy/sell (report 04_mega_switches.md §1, tSort 212/252 =
///     sell = <c>ProcessForInventoryToNPCShop</c>, tSort 215 = buy = <c>ProcessForNPCShopToInventory</c>,
///     both verified against <c>Server/ts25zone/S04_MyWork05.cpp:1398/1716</c>). No I/O, no
///     <see cref="Zone" /> dependency -- money-balance sufficiency and the upper money CAP are deliberately
///     NOT checked here: both are enforced atomically by the SQL layer
///     (<c>usp_Character_AdjustMoneyAndReplaceContainer</c>'s own guards), and the legacy itself Quit()s
///     (disconnects) on either condition for THIS specific pair of actions -- unlike most
///     <c>ProcessForXXX</c> siblings, neither function has a graceful "clean tResult failure" path for
///     insufficient funds, so letting the SQL guard's exception propagate to an <c>Abort</c> at the call
///     site reproduces that exactly, without this policy needing to know the player's current balance.
/// </summary>
/// <remarks>
///     OPEN ISSUES (documented, not guessed): (1) <c>IsRentItem</c>'s hardcoded legacy ID list (rentable
///     items excluded from ordinary buy/sell) has no verified equivalent in Fenrir's <c>world.Items</c>
///     schema (<c>CheckDateItem</c> is a rental-duration day-count, not the same predicate) -- NOT modeled;
///     a rentable item present in an NPC's catalog is bought/sold like any other item. (2) The WarPoint-shop
///     branch (<c>USE_WAR_POINT_SYSTEM</c>, verified ACTIVE for this build) and the Contribution-Point cost
///     (<c>iBuyCost2</c>/<c>aKillOtherTribe</c>) are NOT supported -- an item whose <c>BuyCost2 &gt; 0</c> is
///     rejected as a clean failure rather than silently charging 0 CP. (3) <c>IsValidCostume</c> exclusion on
///     sell is NOT modeled (no costume-id table in Fenrir yet).
/// </remarks>
public static class NpcShopPolicy
{
    /// <summary><c>IsValidTownAll</c> (mapcheck.h:116-128) -- both buy and sell require the CURRENT zone to be one of these.</summary>
    public static readonly IReadOnlySet<short> TownZoneNumbers = new HashSet<short> { 1, 6, 11, 37, 140 };

    /// <summary><c>IRARE</c> (STRUCT.h:1657) -- items of this type or higher (IELITE=4) cannot be sold if enchanted/combined/refined/socketed.</summary>
    private const byte RareItemType = 3;

    /// <summary><c>nType == 13</c> gate on buy (<c>ProcessForNPCShopToInventory</c>) -- requires <c>LV_M1</c> (DEFINE.h:451).</summary>
    public const byte SpecialShopNpcType = 13;

    public const short SpecialShopMinimumLevel = 113;

    public enum SellOutcome
    {
        Success,

        /// <summary><c>iCheckNPCSell == 1</c>, or a Rare/Elite item with any upgrade value applied -- clean failure, no graceful path in the legacy (Quit()-worthy).</summary>
        Rejected,

        /// <summary>Stackable quantity outside [1, <see cref="GroundItemPickupPolicy.MaxStackQuantity" />] or exceeding the held quantity.</summary>
        InvalidQuantity
    }

    public readonly record struct SellResult(
        SellOutcome Outcome,
        long MoneyGained,
        ItemStack? RemainingSourceStack)
    {
        public bool Succeeded => Outcome == SellOutcome.Success;
    }

    /// <summary>
    ///     Ports <c>ProcessForInventoryToNPCShop</c>'s branch on <c>iSort</c> (stackable vs. not) exactly.
    ///     EVERY rejection in the legacy function is a <c>Quit()</c> (no clean <c>*tResult=1</c> path exists
    ///     for this action at all) -- the caller must therefore treat ANY non-<see cref="SellOutcome.Success" />
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
            var remaining = remainingQuantity > 0 ? sourceStack with { Quantity = remainingQuantity } : (ItemStack?)null;
            return new SellResult(SellOutcome.Success, gained, remaining);
        }

        // iType >= IRARE && iValue != 0 (S04_MyWork05.cpp:1508-1513): Fenrir never reassembles the legacy's
        // packed iValue int -- "any of its 4 decomposed bytes nonzero" is this port's documented, closest
        // faithful reading (D8 modeling note), not independently verified byte-for-bit.
        if (item.Type >= RareItemType &&
            (sourceStack.Enchant != 0 || sourceStack.Combine != 0 || sourceStack.Refine != 0 ||
             sourceStack.Socket != 0))
            return new SellResult(SellOutcome.Rejected, 0, sourceStack);

        return new SellResult(SellOutcome.Success, item.SellCost, null);
    }

    public enum BuyOutcome
    {
        Success,

        /// <summary><c>iCheckNPCShop != 2</c> -- clean failure (<c>*tResult=1</c>, NOT a disconnect).</summary>
        NotSellableHere,

        /// <summary>Not present in ANY of this NPC's shop pages/slots -- structural cheat (Quit()-worthy).</summary>
        NotInCatalog,

        /// <summary>Stackable quantity outside [1, <see cref="GroundItemPickupPolicy.MaxStackQuantity" />] -- clean failure.</summary>
        InvalidQuantity,

        /// <summary>Destination occupied by a DIFFERENT item, or a merge would exceed the stack cap, or (non-stackable) destination is occupied at all -- Quit()-worthy.</summary>
        DestinationConflict,

        /// <summary>This item costs Contribution Points (<c>iBuyCost2 &gt; 0</c>) -- NOT supported (see class remarks); clean failure.</summary>
        ContributionCostUnsupported,

        /// <summary><c>nType == 13</c> shop, player below <see cref="SpecialShopMinimumLevel" /> -- Quit()-worthy.</summary>
        BelowMinimumLevel
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

    /// <summary>
    ///     Ports <c>ProcessForNPCShopToInventory</c>'s dispatch exactly (WarPoint-shop branch excluded, see
    ///     class remarks). <paramref name="requestedQuantity" /> is only meaningful for a stackable item.
    ///     <paramref name="currentZoneNumber" /> is the player's CURRENT zone (<c>mSERVER_INFO.mServerNumber</c>,
    ///     <see cref="PlayerRuntimeState.MapId" />) -- feeds <c>CheckBuyCostFree</c>'s 10% discount, which
    ///     applies only on zone 291 (verified dead/unreachable for THIS action, since 291 is not one of
    ///     <see cref="TownZoneNumbers" /> the caller already requires -- kept for source fidelity).
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
    ///     <c>CheckBuyCostFree</c> (function.h:162-187): a 10% discount applies ONLY on zone 291 (not one of
    ///     the 5 town zones this action itself requires -- verified as dead/unreachable code in THIS call
    ///     path, kept for source fidelity rather than silently dropped).
    /// </summary>
    private static int ResolveBuyCost(ItemRowDto item, int quantity, short currentZoneNumber)
    {
        var unitCost = currentZoneNumber == 291 ? (int)(item.BuyCost * 0.9f) : item.BuyCost;
        return ContainerMatrix.IsStackableSort(item.Sort) ? unitCost * quantity : unitCost;
    }
}
