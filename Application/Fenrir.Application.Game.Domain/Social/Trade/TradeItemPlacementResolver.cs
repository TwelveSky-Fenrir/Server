using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>
///     Pure, Zone-independent placement/merge policy for the trade-window item family: deposit
///     (<see cref="ResolveDeposit" />, tSort 218), withdrawal (<see cref="ResolveWithdrawal" />, tSort 219),
///     and rearrangement between two of the caller's own trade slots (<see cref="ResolveRearrange" />,
///     tSort 220). No I/O, no <c>TradeRegistry</c>/<c>Zone</c>/<c>PlayerRuntimeState</c> dependency --
///     independently unit-testable, mirroring <see cref="Fenrir.Application.Game.Domain.Inventory.ContainerMatrix" />
///     and <see cref="Fenrir.Application.Game.Domain.Inventory.GroundItemPickupPolicy" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2136-2246 (<c>ProcessForInventoryToTrade</c>, tSort 218,
///     full body), :2248-2358 (<c>ProcessForTradeToInventory</c>, tSort 219, full body), :2360-2456
///     (<c>ProcessForTradeToTrade</c>, tSort 220, full body) ; :42-53 (<c>CopyGSocket</c>), :72-82
///     (<c>ClearInventory</c>), :93-100 (<c>ClearTrade</c>), :171-192 (<c>INVENTORY_WORK</c>/<c>TRADE_WORK</c>
///     layouts), :214-237 (<c>SetInventory</c>/<c>SetInventoryQuantity</c>) ; Server/Header/Protocol/DEFINE.h:73
///     (<c>USE_MATS_999</c>, unconditional), :287-293 (page/slot bounds), :611 (<c>MAX_ITEM_DUPLICATION_NUM</c>).
///     <para>
///         <b>No-swap rule:</b> this family never swaps -- an occupied, non-mergeable destination is always a
///         hard reject (S04_MyWork05.cpp:2198-2205, 2224-2229 / 2316-2323, 2339-2344 / 2409-2416, 2434-2440),
///         the same reject-on-occupied-destination posture
///         <see cref="Fenrir.Application.Game.Domain.Inventory.ContainerMatrix.ResolveMove" /> itself now
///         follows for its own family. Do not reuse <c>ContainerMatrix.ResolveMove</c> for this family
///         regardless -- the trade window's slot shape and its deposit/withdrawal/rearrange split don't match
///         ContainerMatrix's (from,to) container model.
///     </para>
///     <para>
///         <b>Socket-gem-triple eligibility (the <c>sourceIsSocketEligible</c> parameter on every method below):</b>
///         the legacy behavior contract this class implements documents that <c>CopyGSocket</c> gates the
///         SocketGem1/2/3 transfer on "the item definition is socket-eligible" but does not identify which
///         <c>ItemDefinition</c> field/threshold that corresponds to -- deriving it was explicitly out of
///         scope for the contract. Every resolve method here therefore takes it as a caller-supplied flag
///         rather than re-deriving it from <see cref="ItemDefinition" /> internally; whoever wires this family
///         into a service must source that mapping from a follow-up legacy finding before passing a real
///         value, not guess at it.
///     </para>
///     <para>
///         <b>ExpireDate decision (deliberate deviation, not a guess):</b> the source contract's own "Rent-item
///         expiry date is structurally lost while an item sits in the trade window" edge case explicitly
///         leaves the choice between reproducing that data loss and fixing it to the implementing engineer.
///         This resolver fixes it: a moved/withdrawn/rearranged stack's <c>ExpireDate</c> carries through
///         unchanged on a full-slot move (fresh placement or the entire non-stackable branch) instead of being
///         silently zeroed, because Fenrir's <c>ItemStack</c> (including <c>TradeOfferSide.Slots</c>) has no
///         structural equivalent of the legacy trade-slot's missing expiry field, and silently deleting a
///         paying customer's rent status on trade is a bug worth fixing, not preserving. A stackable merge
///         still never assigns <c>ExpireDate</c> at all (matching <c>SetInventory</c>/<c>SetInventoryQuantity</c>
///         verbatim), so an already-occupied destination's <c>ExpireDate</c> is always left untouched.
///     </para>
///     <para>
///         <b>Partial-stack-split socket fields (documented modeling choice):</b> per the contract's own edge
///         case, a stackable move that leaves quantity behind in the source never touches the destination's
///         socket-gem fields in the legacy code path. Here that means an already-occupied destination keeps
///         its existing SocketGem1/2/3 unchanged, and a fresh (previously-empty) destination starts at
///         all-zero -- the "always-fresh trade slot" reading the contract calls the most likely one.
///     </para>
///     <para>
///         Precondition gates this class does not (re-)implement, because they already exist elsewhere or are
///         one-line checks on state this class doesn't own: the symmetric trade-negotiation-state match
///         (<c>TradeRegistry.TryGetSession</c>) and the "own offer already locked" gate
///         (<c>TradeOfferSide.MenuState != 0</c>, see <c>TradeLockService.TryLock</c>'s identical-shaped
///         check) are the calling service's responsibility, not this resolver's.
///     </para>
/// </remarks>
public static class TradeItemPlacementResolver
{
    public enum PlacementOutcome
    {
        Success,

        /// <summary>No item occupies the source slot at all.</summary>
        SourceEmpty,

        /// <summary>The source slot's item id does not resolve to a known item definition.</summary>
        UnknownItem,

        /// <summary>Deposit only (tSort 218): <c>ITEM_INFO.CheckAvatarTrade == 1</c>.</summary>
        UntradeableItem,

        /// <summary>Stackable items only: requested quantity is outside 1-999, or exceeds the source's own quantity.</summary>
        QuantityOutOfRange,

        /// <summary>Stackable items only: requested quantity exceeds what the source slot currently holds.</summary>
        InsufficientSourceQuantity,

        /// <summary>
        ///     Destination occupied by a different item id (stackable), occupied at all (non-stackable), or a
        ///     stackable merge would exceed 999 -- never a swap, always a reject.
        /// </summary>
        DestinationIncompatible
    }

    /// <summary>MAX_ITEM_DUPLICATION_NUM, DEFINE.h:611.</summary>
    public const int MinQuantity = 1;

    public const int MaxQuantity = 999;

    /// <summary>MAX_TRADE_SLOT_NUM, DEFINE.h:291 (== <see cref="TradeLimits.SlotCount" />).</summary>
    public static bool IsValidTradeSlot(int slot)
    {
        return slot >= 0 && slot < TradeLimits.SlotCount;
    }

    /// <summary>
    ///     MAX_INVENTORY_PAGE_NUM, DEFINE.h:287 -- valid values for tSort 218's source page and tSort 219's
    ///     destination page (220/221/222 never touch an inventory page directly).
    /// </summary>
    public static bool IsValidInventoryPage(int page)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1;
    }

    /// <summary>MAX_INVENTORY_SLOT_NUM, DEFINE.h:288.</summary>
    public static bool IsValidInventorySlot(int slot)
    {
        return slot is >= 0 and <= 63;
    }

    /// <summary>tSort 219's destination grid X/Y coordinates, written straight through (S04_MyWork05.cpp:2265-2270).</summary>
    public static bool IsValidGridCoordinate(int coordinate)
    {
        return coordinate is >= 0 and <= 7;
    }

    /// <summary>
    ///     Gate for touching the second/last inventory page (the "premium/VIP" page): the character's premium
    ///     status must not already be expired (S04_MyWork05.cpp:2149-2157 for 218, :2272-2280 for 219). Takes
    ///     "now" as a parameter instead of reading the clock itself, to stay pure and unit-testable.
    /// </summary>
    public static bool IsPremiumPageAccessAllowed(long premiumExpireUnixSeconds, long nowUnixSeconds)
    {
        return premiumExpireUnixSeconds >= nowUnixSeconds;
    }

    /// <summary>tSort 218 -- ProcessForInventoryToTrade. The only caller of this family that checks CheckAvatarTrade.</summary>
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

    /// <summary>tSort 219 -- ProcessForTradeToInventory. iCheckAvatarTrade is never re-checked on withdrawal.</summary>
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

    /// <summary>tSort 220 -- ProcessForTradeToTrade. Purely within the caller's own 8 trade slots.</summary>
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

        // CopyGSocket only ever runs on full depletion; a partial split leaves the destination's socket-gem
        // fields exactly as they already were (0 for a previously-empty slot).
        var (gem1, gem2, gem3) = fullyEmptied
            ? sourceIsSocketEligible
                ? (source.SocketGem1, source.SocketGem2, source.SocketGem3)
                : (0, 0, 0)
            : (destination?.SocketGem1 ?? 0, destination?.SocketGem2 ?? 0, destination?.SocketGem3 ?? 0);

        // "value" (Enchant/Combine/Refine/Socket-byte) and serial are always reset to 0 on the destination,
        // even when merging onto an already-populated slot (S04_MyWork05.cpp:2213-2216). ExpireDate is never
        // assigned by SetInventory/SetInventoryQuantity for the stackable path in either direction, so the
        // destination's existing ExpireDate (0 if it didn't exist yet) is preserved untouched.
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

        // The whole source slot moves as one unit (item id, quantity, value, serial) -- and, per this
        // resolver's documented ExpireDate decision, its expiry date too. Only the socket-gem triple is
        // gated by socket-eligibility; source is always fully cleared (ClearInventory/ClearTrade).
        var newDestination = source with { SocketGem1 = gem1, SocketGem2 = gem2, SocketGem3 = gem3 };

        return new PlacementResult(PlacementOutcome.Success, null, newDestination);
    }

    /// <summary>NewSource/NewDestination are the values to write back; null means "slot becomes empty".</summary>
    public readonly record struct PlacementResult(
        PlacementOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination)
    {
        public bool Succeeded => Outcome == PlacementOutcome.Success;
    }
}
