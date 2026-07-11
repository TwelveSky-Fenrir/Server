using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     The op23 (CZ_USE_INVENTORY_ITEM_SEND) per-item dispatch registry: resolves an addressed item to exactly
///     one <see cref="IUseItemHandler" />, replacing the legacy's flat ~80-case <c>switch(iIndex)</c> plus its
///     two inlined pre-switch direct handlers and its sort-based equipment branch with a single resolved-once
///     lookup. Consulted by <c>UseInventoryItemService</c> as its terminal dispatch stage: an item no
///     pre-existing service family already claims is offered here, and only falls through to the generic
///     result-1 failure if this registry also declines it.
///     <para>
///         Resolution order (id-keyed direct handlers first, then the category-based equip-swap) is disjoint
///         from every family the service already handles, so consulting it after those families is safe. The
///         id table is a <see cref="FrozenDictionary{TKey,TValue}" /> — built once at construction, read on the
///         request path, never mutated.
///     </para>
///     <para>
///         Families the op23 dispatcher also routes to but that are, by this workstream's (C9) direction,
///         owned elsewhere or not yet routable, are deliberately NOT registered here — an unregistered item
///         still falls through to the service's clean result-1 failure, exactly its behavior before C9:
///         <list type="bullet">
///             <item>Generic openable boxes → workstream C10 (own behavior contract).</item>
///             <item>
///                 Tribe-item families → workstream C11. Item 8100 (forced-neutral tribe reset,
///                 <see cref="ForcedNeutralTribeResetUseItemHandler" />) and items 8153/8154 (faction-transfer
///                 scroll / TChangeTribe, <see cref="TribeScrollTransferUseItemHandler" />) are now registered
///                 directly above; the tribe-conversion BOOK family (99014/99015/99016, TChangeEquip) remains
///                 unrouted here -- it is a genuinely different, all-or-nothing mechanism
///                 (<c>ICharacterRepository.ApplyTribeConversionAsync</c> already exists on the data side with
///                 no Application-layer consumer yet).
///             </item>
///             <item>Tower-item families → workstream A11.</item>
///             <item>
///                 Costume / stellar-core intermediate dispatch → workstream C9-costume-stellar-whitelist. NOW
///                 ROUTED, as a third fallback check tried after the id-keyed dictionary and the equip-swap
///                 category match (<see cref="CostumeStellarCoreUseItemHandler.ClaimsItem" />) -- see that
///                 handler's own remarks for the whitelist tables and the grant mechanics.
///             </item>
///         </list>
///     </para>
/// </summary>
public sealed class UseItemHandlerRegistry
{
    private readonly FrozenDictionary<int, IUseItemHandler> _byId;
    private readonly EquipSwapUseItemHandler _equipSwap;
    private readonly CostumeStellarCoreUseItemHandler _costumeStellarCore;

    public UseItemHandlerRegistry(
        TitleUpgradeUseItemHandler titleUpgrade,
        TitleRemoveScrollUseItemHandler titleRemoveScroll,
        PalaceRankUpgradeUseItemHandler palaceRank,
        EquipSwapUseItemHandler equipSwap,
        LootBoxUseItemHandler lootBox,
        ForcedNeutralTribeResetUseItemHandler forcedNeutralTribeReset,
        TribeScrollTransferUseItemHandler tribeScrollTransfer,
        CpTicketUseItemHandler cpTicket,
        EliteDungeonTicketUseItemHandler eliteDungeonTicket,
        DungeonKeyUseItemHandler dungeonKey,
        IvyHallTicketUseItemHandler ivyHallTicket,
        LuckyTicketUseItemHandler luckyTicket,
        ScrollOfSeekersUseItemHandler scrollOfSeekers,
        CostumeStellarCoreUseItemHandler costumeStellarCore)
    {
        var byId = new Dictionary<int, IUseItemHandler>
        {
            [TitleUpgradeUseItemHandler.ItemId] = titleUpgrade,
            [PalaceRankUpgradeUseItemHandler.ItemId] = palaceRank,
            [ForcedNeutralTribeResetUseItemHandler.ItemId] = forcedNeutralTribeReset,
            [DungeonKeyUseItemHandler.ItemId] = dungeonKey
        };
        // C19: title-remove scroll (items 1200/8419/1494) -- distinct mechanism from the single-id
        // TitleUpgradeUseItemHandler (891) above; do not conflate the two.
        foreach (var id in TitleRemoveScrollUseItemHandler.HandledItemIds)
            byId[id] = titleRemoveScroll;
        // C11: faction-transfer scroll (items 8153/8154) -- client-chosen tribe conversion, distinct from the
        // forced-neutral tribe reset (item 8100) above.
        foreach (var id in TribeScrollTransferUseItemHandler.HandledItemIds)
            byId[id] = tribeScrollTransfer;
        // C10: the loot-box handler claims every box id LootBoxCatalog registers, including 635 as of
        // C10-mountbox635 (601/602/635/2249/7105/8112/76542, ...). MountBoxUseItemHandler (the former
        // item-635 C9 stub) is deleted -- fully superseded by this registration.
        foreach (var id in LootBoxUseItemHandler.HandledItemIds)
            byId[id] = lootBox;
        // C9-tickets-tower: CP Ticket family, Elite Dungeon Ticket family, Ivy Hall Ticket pair, Lucky Ticket
        // family (draw thresholds/tier cascade/family serial resolved by the recovered
        // lucky-ticket-handler-thresholds contract), Scroll of Seekers family (180-vs-900 per-id split
        // resolved by the recovered scroll-of-seekers-per-id-split contract).
        foreach (var id in CpTicketUseItemHandler.HandledItemIds)
            byId[id] = cpTicket;
        foreach (var id in EliteDungeonTicketUseItemHandler.HandledItemIds)
            byId[id] = eliteDungeonTicket;
        foreach (var id in IvyHallTicketUseItemHandler.HandledItemIds)
            byId[id] = ivyHallTicket;
        foreach (var id in LuckyTicketUseItemHandler.HandledItemIds)
            byId[id] = luckyTicket;
        foreach (var id in ScrollOfSeekersUseItemHandler.HandledItemIds)
            byId[id] = scrollOfSeekers;
        _byId = byId.ToFrozenDictionary();
        _equipSwap = equipSwap;
        _costumeStellarCore = costumeStellarCore;
    }

    /// <summary>
    ///     Resolves the handler for an addressed item, or null if this registry does not claim it (the caller
    ///     then falls through to its generic failure). Id-keyed direct handlers win first; then the
    ///     category-based double-click-to-equip handler claims any item resolving to a real equip slot;
    ///     finally the costume/stellar-core whitelist fallback (workstream C9-costume-stellar-whitelist)
    ///     claims any remaining item id present in either <see cref="CostumeStellarCoreWhitelist" /> table --
    ///     tried last since costume/stellar core ids are not a fixed small set suited to id-keyed dispatch.
    /// </summary>
    public IUseItemHandler? Resolve(ItemStack item, ItemDefinition definition)
    {
        if (_byId.TryGetValue(item.ItemId, out var handler))
            return handler;

        if (EquipSwapUseItemHandler.ClaimsItem(definition.Item))
            return _equipSwap;

        if (CostumeStellarCoreUseItemHandler.ClaimsItem(item.ItemId))
            return _costumeStellarCore;

        return null;
    }
}
