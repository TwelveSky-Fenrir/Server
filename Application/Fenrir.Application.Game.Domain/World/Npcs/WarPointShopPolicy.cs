using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Npcs;

/// <summary>
///     Pure, Zone-independent resolution of a War-Point NPC-shop purchase (the <c>USE_WAR_POINT_SYSTEM</c>
///     branch of <c>ProcessForNPCShopToInventory</c>, <c>Server/ts25zone/S04_MyWork05.cpp:1798-1988</c>), sibling
///     to <see cref="NpcShopPolicy" />. Decides everything a War-Point purchase needs <b>except</b> War-Point
///     affordability itself: the War-Point balance is not reliably mirrored on
///     <see cref="PlayerRuntimeState" /> (it is not hydrated at world entry, see
///     <c>PlayerRuntimeState.WarPoint</c>'s remarks), so its sufficiency is enforced server-authoritatively and
///     atomically by the SQL layer (<c>usp_Character_BuyWarPointItem</c>) instead -- the same posture
///     <see cref="NpcShopPolicy" /> uses for wallet Money. Contribution-Point sufficiency, by contrast, IS
///     checked here, because <see cref="PlayerRuntimeState.ContributionPoints" /> already lives in memory (same
///     split <see cref="NpcShopPolicy" /> already applies for the ordinary silver+CP path).
/// </summary>
/// <remarks>
///     The defining War-Point behaviors reproduced here (<c>S04_MyWork05.cpp:1798-1895,1940-1987</c>): a
///     War-Point item BYPASSES both the ordinary <c>iCheckNPCShop == 2</c> shop-membership requirement and the
///     rent-item block a normal purchase must pass (a War-Point item is validated by the price table alone);
///     a stackable quantity below 1 is COERCED UP to 1 rather than rejected (contrast the ordinary path, which
///     soft-rejects it); insufficient currency is a SOFT rejection (never a disconnect), whereas a
///     destination-slot conflict or a wrong-NPC request is a hard reject (disconnect). Non-stackable ids
///     <c>86700</c>-<c>86725</c> are granted in legacy with a fixed item-state stamp
///     (<c>S04_MyWork05.cpp:1971-1977</c>); that stamp's byte value is not in this workstream's contract, so
///     those ids are granted plain here and flagged (see <see cref="IsFixedStateStampItem" />). A non-stackable
///     purchase always delivers exactly one unit regardless of the requested quantity, but the CHARGED cost is
///     still scaled by that quantity (<c>WarPointSystem.h:207-213</c>, <c>S04_MyWork05.cpp:1971-1978</c> vs. the
///     hardcoded-0 <c>SetInventory</c> quantity argument at the same call sites) -- see <see cref="Cost" />'s own
///     remarks for the exact non-positive-quantity floor.
/// </remarks>
public static class WarPointShopPolicy
{
    public enum BuyOutcome
    {
        /// <summary>
        ///     NPC is not a War-Point NPC, or the item is not in the global price table -- not a War-Point
        ///     transaction. The caller falls through to the ordinary NPC-shop purchase path.
        /// </summary>
        NotWarPointItem,

        /// <summary>
        ///     Resolved: <see cref="BuyResolution.WarPointCost" />/<see cref="BuyResolution.ContributionPointCost" />/
        ///     <see cref="BuyResolution.NewDestinationStack" /> are set. The caller executes the atomic War-Point
        ///     purchase (which re-checks War-Point affordability server-side).
        /// </summary>
        Proceed,

        /// <summary>
        ///     The item is a War-Point item but is not displayed at this NPC's grid (page-2 "Purple" tribe gating,
        ///     including any such request at <see cref="WarPointShopCatalog.NangimNpcId" />) -- the legacy
        ///     disconnects before any War-Point logic runs (<c>S04_MyWork05.cpp:1769-1788</c>). Hard reject.
        /// </summary>
        WrongNpc,

        /// <summary>
        ///     Destination occupied by a different item, an overflowing merge, or (non-stackable) an occupied
        ///     destination (<c>S04_MyWork05.cpp:1847-1861,1941-1946</c>). Hard reject (disconnect).
        /// </summary>
        DestinationConflict,

        /// <summary>
        ///     <c>wAvatar.aKillOtherTribe &lt; tCostCP</c> -- not enough Contribution Points for this item's
        ///     Contribution-Point price. SOFT reject, no disconnect (<c>S04_MyWork05.cpp:1867-1873</c>).
        /// </summary>
        InsufficientContributionPoints
    }

    /// <summary>Legacy <c>86700</c>-<c>86725</c> fixed-item-state-stamp band lower bound.</summary>
    private const int FixedStateStampRangeStart = 86700;

    /// <summary>Legacy <c>86700</c>-<c>86725</c> fixed-item-state-stamp band upper bound (inclusive).</summary>
    private const int FixedStateStampRangeEndInclusive = 86725;

    /// <summary>
    ///     True for the non-stackable <c>86700</c>-<c>86725</c> band the legacy grants with a fixed item-state
    ///     stamp. Currently informational only: the stamp's byte value is a data gap, so these ids are still
    ///     granted plain (see class remarks).
    /// </summary>
    public static bool IsFixedStateStampItem(int itemId)
    {
        return itemId is >= FixedStateStampRangeStart and <= FixedStateStampRangeEndInclusive;
    }

    /// <summary>
    ///     Resolves a candidate NPC-shop purchase against the War-Point table.
    ///     <paramref name="destinationSlot" /> is the current content of the requested destination slot (null =
    ///     empty). <paramref name="playerContributionPoints" /> is
    ///     <see cref="PlayerRuntimeState.ContributionPoints" /> at request time.
    /// </summary>
    public static BuyResolution ResolveBuy(WarPointShopCatalog catalog, int npcId, ItemDefinition itemDefinition,
        int requestedQuantity, ItemStack? destinationSlot, int playerContributionPoints)
    {
        var item = itemDefinition.Item;

        // War-Point flagging: only a War-Point NPC selling an item present in the global price table.
        if (!WarPointShopCatalog.IsWarPointNpc(npcId) || !catalog.TryGetPrice(item.ItemId, out var price))
            return new BuyResolution(BuyOutcome.NotWarPointItem, 0, 0, null);

        // Per-NPC display-grid gate (page-2 tribe items display at exactly one NPC; never at Nangin).
        if (!price.DisplaysAtNpc(npcId))
            return new BuyResolution(BuyOutcome.WrongNpc, 0, 0, null);

        if (ContainerMatrix.IsStackableSort(item.Sort))
        {
            // War-Point stackable: a quantity below 1 is coerced up to 1 (never rejected).
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

        // Non-stackable: a non-empty destination is a hard reject.
        if (destinationSlot is not null)
            return new BuyResolution(BuyOutcome.DestinationConflict, 0, 0, null);

        // 86700-86725 would carry a fixed item-state stamp in legacy; its byte value is unknown (data gap), so
        // the item is granted plain -- IsFixedStateStampItem marks the gap without inventing a stamp.
        // requestedQuantity (not a hardcoded 1) is passed through deliberately: only exactly ONE unit is ever
        // delivered on the non-stackable branch, but legacy still scales the CHARGED cost by whatever quantity
        // was requested (WarPointSystem.h:207-213) -- see Cost's own remarks for the exact scaling rule.
        return Cost(price, requestedQuantity, playerContributionPoints,
            new ItemStack(item.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>
    ///     Scales both prices by <paramref name="quantity" /> and gates on Contribution-Point sufficiency.
    ///     War-Point sufficiency is deliberately NOT gated here -- the atomic SQL purchase enforces it (see class
    ///     summary).
    /// </summary>
    /// <remarks>
    ///     Legacy only multiplies the per-unit price by <paramref name="quantity" /> when it is positive; a zero
    ///     or negative quantity leaves the unscaled, single-unit price in effect instead of zeroing it out
    ///     (<c>WarPointSystem.h:207-213</c>). The stackable branch above already floors its own quantity to at
    ///     least 1 before calling here, so this only has an observable effect through the non-stackable branch,
    ///     which passes its raw, unfloored <c>requestedQuantity</c> straight through.
    /// </remarks>
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
