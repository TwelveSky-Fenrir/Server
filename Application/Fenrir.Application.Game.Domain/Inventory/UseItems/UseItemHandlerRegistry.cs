using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class UseItemHandlerRegistry
{
    private readonly FrozenDictionary<int, IUseItemHandler> _byId;
    private readonly CostumeStellarCoreUseItemHandler _costumeStellarCore;
    private readonly EquipSwapUseItemHandler _equipSwap;

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
        foreach (var id in TitleRemoveScrollUseItemHandler.HandledItemIds)
            byId[id] = titleRemoveScroll;
        foreach (var id in TribeScrollTransferUseItemHandler.HandledItemIds)
            byId[id] = tribeScrollTransfer;
        foreach (var id in LootBoxUseItemHandler.HandledItemIds)
            byId[id] = lootBox;
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
