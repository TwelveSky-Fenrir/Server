using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
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

        var offlineItems = new List<OfflineShopItemSlotTvp>();
        var claimedSourceSlots = new HashSet<int>();
        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot);
            if (!view.IsOccupied)
                continue;

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

            worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition);
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
                    view.ItemId, view.Quantity, view.Value, view.Serial, view.Price,
                    PshopPurchasePolicy.EncodeSocketData(liveSlot!.Value.SocketGem1, liveSlot.Value.SocketGem2,
                        liveSlot.Value.SocketGem3)));
        }

        var uniqueNumber = unchecked((uint)(state.CharacterId * 2 + (isProxy ? 1 : 0)));
        var listing = packet.PshopInfo with { UniqueNumber = uniqueNumber };

        if (!isProxy)
        {
            state.PshopOpen = true;
            state.PshopListing = listing;
            logger.LogInformation(
                "Personal shop opened: character {CharacterId} name {ShopName} ({OccupiedSlots} occupied slots, display-only, no items left inventory)",
                state.CharacterId, listing.Name, CountOccupiedSlots(listing));
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.LiveOpened,
                new OpenShopStallResponse { Result = 0, PshopInfo = listing }, listing, null);
        }

        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.ProxyReady, null, listing, offlineItems);
    }

    public async ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken)
    {
        if (zone.ProxyShopCount >= Zone.MaxProxyShopSlots)
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} hit the global {Cap}-slot proxy-shop capacity ceiling",
                characterId, Zone.MaxProxyShopSlots);
            return new OpenShopStallResponse { Result = 105, PshopInfo = listing };
        }

        OfflineShopRowDto? existing;
        try
        {
            (existing, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} proxy-shop rental-credit read failed while opening a proxy shop",
                characterId);
            return new OpenShopStallResponse { Result = 103, PshopInfo = listing };
        }

        var shopDate = existing?.ShopDate ?? 0;

        if (shopDate < GameDate.Today())
        {
            logger.LogInformation(
                "Proxy shop open rejected: character {CharacterId} has no unexpired rental credit " +
                "(game.OfflineShops.ShopDate {ShopDate})",
                characterId, shopDate);
            return new OpenShopStallResponse { Result = 104, PshopInfo = listing };
        }

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        var auditEntries = new List<ProxyShopListAuditEntry>();

        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot);
            if (!view.IsOccupied)
                continue;

            var sourceContainer = view.InventoryPage == ContainerMatrix.InventoryPage0 ? page0 : page1;
            if (sourceContainer.TryGetValue((byte)view.InventoryIndex, out var movedStack))
                auditEntries.Add(new ProxyShopListAuditEntry(view.ItemId, view.Quantity, view.Value, view.Serial,
                    view.Price, movedStack.SocketGem1, movedStack.SocketGem2, movedStack.SocketGem3));

            if (view.InventoryPage == ContainerMatrix.InventoryPage0)
                page0 = page0.Remove((byte)view.InventoryIndex);
            else
                page1 = page1.Remove((byte)view.InventoryIndex);
        }

        try
        {
            await offlineShops.OpenAndReplaceContainersAsync(characterId, zone.MapId, shopDate, packet.PshopInfo.Name,
                (int)state.PosX, (int)state.PosY, (int)state.PosZ, offlineItems,
                ToTvps(page0), ToTvps(page1), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop open OpenAndReplaceContainersAsync failed (treated as already open)",
                characterId);
            return new OpenShopStallResponse { Result = 102, PshopInfo = listing };
        }

        foreach (var entry in auditEntries)
            await eventLog.LogAsync(ProxyShopListEventCode, EventLogCategory.ProxyShop, accountId, characterId,
                null, null, null, null, null, entry.ItemId, entry.Quantity, 1,
                $"Value={entry.Value};Serial={entry.Serial};Price={entry.Price};Socket1={entry.Socket1};Socket2={entry.Socket2};Socket3={entry.Socket3}",
                cancellationToken);

        logger.LogInformation(
            "Proxy shop opened: character {CharacterId} name {ShopName} rental until {ShopDate} ({ItemCount} items moved out of inventory)",
            characterId, listing.Name, shopDate, auditEntries.Count);

        var response = new OpenShopStallResponse { Result = 0, PshopInfo = listing };

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

        return response;
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
