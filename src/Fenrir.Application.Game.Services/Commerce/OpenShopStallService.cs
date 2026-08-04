using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class OpenShopStallService(
    IOfflineShopRepository offlineShops,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<OpenShopStallService> logger) : IOpenShopStallService
{
    private const int IdleActionSort = 1;

    private const short ProxyShopListEventCode = 1;

    public async ValueTask<OpenShopStallPrepareResult> PrepareAsync(OpenShopStallRequest packet,
        PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        using var shopLease = await PersonalShopBusinessLock.AcquireAsync(state.CharacterId, cancellationToken);

        if (packet.Sort is not (1 or 2))
            return Abort(state.CharacterId, $"invalid sort value {packet.Sort}");

        if (!ProxyShopZonePolicy.IsWithinMarketDistrict(state.PosX, state.PosY, state.PosZ))
            return Abort(state.CharacterId, "outside the market district");

        if (state.ActionSort != IdleActionSort)
            return Abort(state.CharacterId, $"not stationary/idle (action sort {state.ActionSort})");

        var isProxy = packet.Sort == 2;

        if (state.PshopOpen)
        {
            if (isProxy)
                return Blocked(101, packet.PshopInfo, state.CharacterId,
                    "a live personal shop is already open (cross-type proxy request)");

            return Abort(state.CharacterId, "a live personal shop is already open (same-type request)");
        }

        if (!isProxy)
        {
            OfflineShopRowDto? proxyShop;
            try
            {
                (proxyShop, _) = await offlineShops.GetByCharacterAsync(state.CharacterId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} proxy-shop-state round trip failed while opening a personal shop",
                    state.CharacterId);
                return Blocked(103, packet.PshopInfo, state.CharacterId, "proxy-shop-state round trip failed");
            }

            if (proxyShop is { ShopState: 1 })
                return Blocked(102, packet.PshopInfo, state.CharacterId,
                    "a proxy/deputy shop is already open (cross-type personal request)");
        }

        if (string.IsNullOrWhiteSpace(packet.PshopInfo.Name))
            return Abort(state.CharacterId, "empty shop name");

        var anyOccupied = false;
        for (var page = 0; page < PshopPurchasePolicy.MaxPages && !anyOccupied; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
            if (PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot).IsOccupied)
            {
                anyOccupied = true;
                break;
            }

        if (!anyOccupied)
            return Abort(state.CharacterId, "no occupied slots submitted");

        IReadOnlyList<OfflineShopItemRowDto> existingProxyItems = [];
        if (isProxy)
            try
            {
                (_, existingProxyItems) = await offlineShops.GetByCharacterAsync(state.CharacterId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} proxy-shop listing read failed while preparing an open request",
                    state.CharacterId);
                return Blocked(103, packet.PshopInfo, state.CharacterId,
                    "proxy-shop listing read failed while preparing an open request");
            }

        var offlineItems = new List<OfflineShopItemSlotTvp>();
        var claimedSourceSlots = new HashSet<int>();
        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot);
            if (!view.IsOccupied)
                continue;

            if (!worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition) ||
                itemDefinition.Item.CheckAvatarShop == 1 ||
                view.Price is < 1 or > PshopPurchasePolicy.MaxSellPrice ||
                (ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort) &&
                 view.Quantity is < 1 or > GroundItemPickupPolicy.MaxStackQuantity))
                return Abort(state.CharacterId, $"slot {page}/{slot} has invalid item or sale values");

            var continuesProxyListing = isProxy && (view.InventoryPage == -1 || view.InventoryIndex == -1);
            if (continuesProxyListing)
            {
                var slotIndex = (short)(page * PshopPurchasePolicy.MaxSlots + slot);
                var existing = existingProxyItems.FirstOrDefault(item => item.SlotIndex == slotIndex);
                if (existing is null || existing.ItemId != view.ItemId || existing.Quantity != view.Quantity ||
                    existing.Value != view.Value || existing.SocketGem1 != view.SocketGem1 ||
                    existing.SocketGem2 != view.SocketGem2 || existing.SocketGem3 != view.SocketGem3)
                    return Abort(state.CharacterId,
                        $"slot {page}/{slot} does not match the existing proxy-shop listing");

                offlineItems.Add(new OfflineShopItemSlotTvp(slotIndex, view.ItemId, view.Quantity, view.Value,
                    view.Serial, view.Price, null, view.SocketGem1, view.SocketGem2, view.SocketGem3));
                continue;
            }

            if (view.InventoryPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
                !ContainerMatrix.IsValidSlot((byte)view.InventoryPage, view.InventoryIndex) ||
                view.PosX is < 0 or > 7 || view.PosY is < 0 or > 7)
                return Abort(state.CharacterId, $"slot {page}/{slot} has invalid inventory coordinates");

            if (!claimedSourceSlots.Add(view.InventoryPage * ContainerMatrix.InventoryPageSlotCount +
                                        view.InventoryIndex))
                return Abort(state.CharacterId,
                    $"slot {page}/{slot} re-uses inventory {view.InventoryPage}/{view.InventoryIndex}");

            if (view.InventoryPage == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today())
                return Abort(state.CharacterId, $"slot {page}/{slot} references the expired dated-vault last page");

            var liveSlot = state.Inventory.GetSlot((byte)view.InventoryPage, (byte)view.InventoryIndex);

            if (PshopPurchasePolicy.ValidateOpenSlot(view, itemDefinition, liveSlot) !=
                PshopPurchasePolicy.OpenSlotOutcome.Success)
                return Abort(state.CharacterId, $"slot {page}/{slot} failed live-inventory validation");

            if (liveSlot!.Value.XPos != view.PosX || liveSlot.Value.YPos != view.PosY)
                return Abort(state.CharacterId,
                    $"slot {page}/{slot} declared grid position ({view.PosX},{view.PosY}) does not match the live " +
                    $"stack at inventory {view.InventoryPage}/{view.InventoryIndex} " +
                    $"({liveSlot.Value.XPos},{liveSlot.Value.YPos})");

            if (liveSlot.Value.SocketGem1 != view.SocketGem1 || liveSlot.Value.SocketGem2 != view.SocketGem2 ||
                liveSlot.Value.SocketGem3 != view.SocketGem3)
                return Abort(state.CharacterId,
                    $"slot {page}/{slot} declared sockets that do not match the live stack");

            if (isProxy)
                offlineItems.Add(new OfflineShopItemSlotTvp((short)(page * PshopPurchasePolicy.MaxSlots + slot),
                    view.ItemId, view.Quantity, view.Value, view.Serial, view.Price, null,
                    liveSlot.Value.SocketGem1, liveSlot.Value.SocketGem2, liveSlot.Value.SocketGem3));
        }

        if (isProxy)
            foreach (var existing in existingProxyItems)
                if (existing.SlotIndex is >= 0 and < ProxyShopWireMapper.MaxSlots && existing.ItemId is { } itemId &&
                    !offlineItems.Any(item => item.SlotIndex == existing.SlotIndex))
                    offlineItems.Add(new OfflineShopItemSlotTvp(existing.SlotIndex, itemId, existing.Quantity,
                        existing.Value, existing.SerialNumber, existing.Price, existing.SocketData,
                        existing.SocketGem1, existing.SocketGem2, existing.SocketGem3));

        var uniqueNumber = unchecked((uint)(state.CharacterId * 2 + (isProxy ? 1 : 0)));
        var listing = packet.PshopInfo with { UniqueNumber = uniqueNumber };

        if (!isProxy)
        {
            logger.LogInformation(
                "Personal shop prepared: character {CharacterId} name {ShopName} ({OccupiedSlots} occupied slots)",
                state.CharacterId, listing.Name, CountOccupiedSlots(listing));
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.LiveOpened,
                new OpenShopStallResponse { Result = 0, PshopInfo = listing }, listing, null);
        }

        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.ProxyReady, null, listing, offlineItems);
    }

    public async ValueTask<OpenProxyShopOpenResult> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken)
    {
        using var shopLease = await PersonalShopBusinessLock.AcquireAsync(characterId, cancellationToken);

        if (state.CharacterId != characterId || state.PshopOpen || state.ActionSort != IdleActionSort ||
            !ProxyShopZonePolicy.IsWithinMarketDistrict(state.PosX, state.PosY, state.PosZ))
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} changed state after its listing was prepared",
                characterId);
            return Failure(101, listing);
        }

        OfflineShopRowDto? existing;
        IReadOnlyList<OfflineShopItemRowDto> existingItems;
        try
        {
            (existing, existingItems) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} proxy-shop rental-credit read failed while opening a proxy shop",
                characterId);
            return Failure(103, listing);
        }

        if (existing is { ShopState: 1 })
        {
            logger.LogInformation("Proxy shop open rejected: character {CharacterId} already has an open shop",
                characterId);
            return Failure(102, listing);
        }

        if (zone.ProxyShopCount >= Zone.MaxProxyShopSlots)
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} hit the global {Cap}-slot proxy-shop capacity ceiling",
                characterId, Zone.MaxProxyShopSlots);
            return Failure(105, listing);
        }

        var shopDate = existing?.ShopDate ?? 0;

        if (shopDate < GameDate.Today())
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} has no unexpired rental credit " +
                "(game.OfflineShops.ShopDate {ShopDate})",
                characterId, shopDate);
            return Failure(104, listing);
        }

        if (!TryBuildCurrentOfflineItems(packet.PshopInfo, state, existingItems, out var currentOfflineItems))
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} listing or stock changed after it was prepared",
                characterId);
            return Failure(103, listing);
        }

        if (!OfflineItemsMatch(offlineItems, currentOfflineItems))
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} prepared listing is stale",
                characterId);
            return Failure(103, listing);
        }

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var auditEntries = new List<ProxyShopListAuditEntry>();

        var clearedSlots = new List<ProxyShopInventoryClear>();
        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot);
            if (!view.IsOccupied)
                continue;

            if (view.InventoryPage == -1 || view.InventoryIndex == -1)
                continue;

            var sourceContainer = view.InventoryPage == ContainerMatrix.InventoryPage0 ? page0 : page1;
            if (sourceContainer.TryGetValue((byte)view.InventoryIndex, out var movedStack))
                auditEntries.Add(new ProxyShopListAuditEntry(view.ItemId, view.Quantity, view.Value, view.Serial,
                    view.Price, movedStack.SocketGem1, movedStack.SocketGem2, movedStack.SocketGem3));

            if (view.InventoryPage == ContainerMatrix.InventoryPage0)
                page0 = page0.Remove((byte)view.InventoryIndex);
            else
                page1 = page1.Remove((byte)view.InventoryIndex);

            clearedSlots.Add(new ProxyShopInventoryClear(view.InventoryPage, view.InventoryIndex));
        }

        try
        {
            await offlineShops.OpenAndReplaceContainersAsync(characterId, zone.MapId, shopDate, packet.PshopInfo.Name,
                (int)state.PosX, (int)state.PosY, (int)state.PosZ, currentOfflineItems,
                ToTvps(page0), ToTvps(page1), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop open OpenAndReplaceContainersAsync failed (treated as already open)",
                characterId);
            return Failure(102, listing);
        }

        foreach (var entry in auditEntries)
            await eventLog.LogAsync(ProxyShopListEventCode, EventLogCategory.ProxyShop, accountId, characterId,
                null, null, null, null, null, entry.ItemId, entry.Quantity, 1,
                $"Value={entry.Value};Serial={entry.Serial};Price={entry.Price};Socket1={entry.Socket1};Socket2={entry.Socket2};Socket3={entry.Socket3}",
                cancellationToken);

        logger.LogInformation(
            "Proxy shop opened: character {CharacterId} name {ShopName} rental until {ShopDate} ({ItemCount} items moved out of inventory)",
            characterId, listing.Name, shopDate, auditEntries.Count);

        var openedShop = existing is null
            ? new OfflineShopRowDto(characterId, zone.MapId, 1, shopDate, 0, 0, (int)state.PosX, (int)state.PosY,
                (int)state.PosZ, listing.Name)
            : existing with
            {
                ZoneNumber = zone.MapId,
                ShopState = 1,
                ShopDate = shopDate,
                LocationX = (int)state.PosX,
                LocationY = (int)state.PosY,
                LocationZ = (int)state.PosZ,
                ShopName = listing.Name
            };

        var proxyUser = ProxyShopWireMapper.BuildFromSlots(state.Name, openedShop, currentOfflineItems);
        var snapshot = new GetProxyShopResponse { Result = 0, Sort = 0, ProxyUser = proxyUser };
        var response = new OpenShopStallResponse { Result = 100, PshopInfo = listing };

        zone.RegisterProxyShop(new ProxyShopBroadcastEntry(characterId, unchecked((int)listing.UniqueNumber),
            state.Name, listing.Name, state.PosX, state.PosY, state.PosZ, shopDate));

        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, page0),
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, page1));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop-open mirror for character {CharacterId}",
                zone.MapId, characterId);

        return new OpenProxyShopOpenResult(response, snapshot, clearedSlots);
    }

    private static OpenProxyShopOpenResult Failure(int result, PshopInfo listing)
    {
        return new OpenProxyShopOpenResult(new OpenShopStallResponse { Result = result, PshopInfo = listing }, null,
            []);
    }

    private static int CountOccupiedSlots(PshopInfo listing)
    {
        var count = 0;
        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
            if (PshopPurchasePolicy.ReadSlot(listing, page, slot).IsOccupied)
                count++;
        return count;
    }

    private bool TryBuildCurrentOfflineItems(PshopInfo listing, PlayerRuntimeState state,
        IReadOnlyList<OfflineShopItemRowDto> existingItems, out List<OfflineShopItemSlotTvp> items)
    {
        items = [];
        var claimedSourceSlots = new HashSet<int>();

        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(listing, page, slot);
            if (!view.IsOccupied)
                continue;

            if (!worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition) ||
                itemDefinition.Item.CheckAvatarShop == 1 ||
                view.Price is < 1 or > PshopPurchasePolicy.MaxSellPrice ||
                (ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort) &&
                 view.Quantity is < 1 or > GroundItemPickupPolicy.MaxStackQuantity))
                return false;

            var listingSlot = (short)(page * PshopPurchasePolicy.MaxSlots + slot);
            if (view.InventoryPage == -1 || view.InventoryIndex == -1)
            {
                var existing = existingItems.FirstOrDefault(item => item.SlotIndex == listingSlot);
                if (existing is null || existing.ItemId != view.ItemId || existing.Quantity != view.Quantity ||
                    existing.Value != view.Value || existing.SerialNumber != view.Serial ||
                    existing.SocketGem1 != view.SocketGem1 || existing.SocketGem2 != view.SocketGem2 ||
                    existing.SocketGem3 != view.SocketGem3)
                    return false;

                items.Add(new OfflineShopItemSlotTvp(listingSlot, existing.ItemId, existing.Quantity, existing.Value,
                    existing.SerialNumber, view.Price, null, existing.SocketGem1,
                    existing.SocketGem2, existing.SocketGem3));
                continue;
            }

            if (view.InventoryPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
                !ContainerMatrix.IsValidSlot((byte)view.InventoryPage, view.InventoryIndex) ||
                view.PosX is < 0 or > 7 || view.PosY is < 0 or > 7 ||
                !claimedSourceSlots.Add(view.InventoryPage * ContainerMatrix.InventoryPageSlotCount +
                                        view.InventoryIndex) ||
                (view.InventoryPage == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today()))
                return false;

            var liveSlot = state.Inventory.GetSlot((byte)view.InventoryPage, (byte)view.InventoryIndex);
            if (PshopPurchasePolicy.ValidateOpenSlot(view, itemDefinition, liveSlot) !=
                PshopPurchasePolicy.OpenSlotOutcome.Success ||
                liveSlot!.Value.XPos != view.PosX || liveSlot.Value.YPos != view.PosY ||
                liveSlot.Value.Serial != view.Serial || liveSlot.Value.SocketGem1 != view.SocketGem1 ||
                liveSlot.Value.SocketGem2 != view.SocketGem2 || liveSlot.Value.SocketGem3 != view.SocketGem3)
                return false;

            items.Add(new OfflineShopItemSlotTvp(listingSlot, view.ItemId, view.Quantity, view.Value, view.Serial,
                view.Price, null, liveSlot.Value.SocketGem1, liveSlot.Value.SocketGem2, liveSlot.Value.SocketGem3));
        }

        foreach (var existing in existingItems)
            if (existing.SlotIndex is >= 0 and < ProxyShopWireMapper.MaxSlots && existing.ItemId is { } itemId &&
                !items.Any(item => item.SlotIndex == existing.SlotIndex))
                items.Add(new OfflineShopItemSlotTvp(existing.SlotIndex, itemId, existing.Quantity, existing.Value,
                    existing.SerialNumber, existing.Price, existing.SocketData, existing.SocketGem1,
                    existing.SocketGem2, existing.SocketGem3));

        return true;
    }

    private static bool OfflineItemsMatch(IReadOnlyList<OfflineShopItemSlotTvp> prepared,
        IReadOnlyList<OfflineShopItemSlotTvp> current)
    {
        if (prepared.Count != current.Count)
            return false;

        foreach (var item in prepared)
            if (!current.Contains(item))
                return false;

        return true;
    }

    private OpenShopStallPrepareResult Abort(int characterId, string reason)
    {
        logger.LogWarning(
            "Open shop stall rejected: character {CharacterId} session will be disconnected ({Reason})",
            characterId, reason);
        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);
    }

    private OpenShopStallPrepareResult Blocked(int result, PshopInfo pshopInfo, int characterId, string reason)
    {
        logger.LogInformation("Open shop stall blocked: character {CharacterId} result {Result} ({Reason})",
            characterId, result, reason);
        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Blocked,
            new OpenShopStallResponse { Result = result, PshopInfo = pshopInfo }, default, null);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private readonly record struct ProxyShopListAuditEntry(
        int ItemId,
        int Quantity,
        int Value,
        int Serial,
        int Price,
        int Socket1,
        int Socket2,
        int Socket3);
}
