using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

/// <remarks>
///     The legacy's rental expiration column has no Fenrir equivalent -- a Fenrir-invented window is used
///     instead, admin-tunable via <c>ProxyShopDurationDays</c>.
/// </remarks>
public sealed class OpenShopStallService(
    IOfflineShopRepository offlineShops,
    IGameSettingsRepository gameSettings,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<OpenShopStallService> logger) : IOpenShopStallService
{
    /// <summary>
    ///     mDATA.aAction.aSort's idle/ready pose sentinel -- the same value already independently established
    ///     by <c>Zone</c>'s own private <c>IdleActionSort</c> (Zone.Stun.cs) and by the identical
    ///     <c>ActionSort != 1</c> gate <c>MountStateResolver</c> and <c>GenericActionService</c> already apply
    ///     for their own, unrelated actions.
    /// </summary>
    private const int IdleActionSort = 1;

    /// <summary>
    ///     game.EventLog.EventCode for a proxy-shop listing row (legacy <c>GL_1000_PXSHOP_REG</c>), scoped
    ///     within <see cref="EventLogCategory.ProxyShop" /> -- see that enum member's remarks for the full
    ///     1-4 numbering.
    /// </summary>
    private const short ProxyShopListEventCode = 1;

    public async ValueTask<OpenShopStallPrepareResult> PrepareAsync(OpenShopStallRequest packet,
        PlayerRuntimeState state, CancellationToken cancellationToken)
    {
        if (packet.Sort is not (1 or 2))
            return Abort(state.CharacterId, $"invalid sort value {packet.Sort}");

        // Server/ts25zone/S04_MyWork02.cpp:6056-6060 (mapcheck.h:189-244 CheckPossiblePShopRegion, zone-37
        // case) -- the requester must be physically inside the fixed-center, 1000-unit-radius "market
        // district" before EITHER shop type may open. Checked once here, ahead of the stationary-action-state
        // gate below, exactly matching the legacy ordering; applies identically to both sort values (the
        // legacy call site is made once, before the live/proxy handling paths diverge). Legacy disconnects
        // the session outright on failure, with no response of any kind -- the same harsh treatment Abort()
        // below already gives the adjacent invalid-sort-value and not-stationary guards.
        if (!ProxyShopZonePolicy.IsWithinMarketDistrict(state.PosX, state.PosY, state.PosZ))
            return Abort(state.CharacterId, "outside the market district");

        // Server/ts25zone/S04_MyWork02.cpp:6061-6065 -- the character must be recorded as stationary/idle
        // (an exact match against the idle-pose sentinel) before EITHER shop type is allowed to open. Checked
        // once here, ahead of the proxy/personal branch below and every later gate (already-open, name,
        // per-slot inventory), exactly matching the legacy ordering. Legacy disconnects the session outright
        // on any other recorded value -- the same harsh treatment the two gates immediately around it in the
        // legacy handler apply, which Fenrir already mirrors via the shared Abort outcome below.
        if (state.ActionSort != IdleActionSort)
            return Abort(state.CharacterId, $"not stationary/idle (action sort {state.ActionSort})");

        var isProxy = packet.Sort == 2;

        // Server/ts25zone/S04_MyWork02.cpp:6067-6078 -- the live/personal-shop-open flag blocks a request of
        // EITHER type while it is set: a cross-type PROXY request gets a coded, non-disconnecting response
        // (101); a same-type PERSONAL request is disconnected outright (Quit()), the same harsh treatment the
        // two gates immediately around it in the legacy handler apply.
        if (state.PshopOpen)
        {
            if (isProxy)
                return Blocked(101, packet.PshopInfo, state.CharacterId,
                    "a live personal shop is already open (cross-type proxy request)");

            return Abort(state.CharacterId, "a live personal shop is already open (same-type request)");
        }

        if (!isProxy)
        {
            // Server/ts25zone/S04_MyWork02.cpp:6080-6095 -- opening a PERSONAL shop synchronously round-trips
            // the proxy/offline-shop state (legacy: a ts25extra IPC call via
            // U_ZONE_GET_PROXY_STATE_FOR_EXTRA_SEND; Fenrir: the repository the offline-shop feature itself
            // persists through). An IPC/round-trip failure (103) and a confirmed-active proxy shop (102) are
            // distinct legacy failure modes and must stay distinguishable here too.
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

        // Validate every occupied slot against the LIVE inventory before touching anything.
        var offlineItems = new List<OfflineShopItemSlotTvp>();
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

            worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition);
            var liveSlot = state.Inventory.GetSlot((byte)view.InventoryPage, (byte)view.InventoryIndex);

            if (PshopPurchasePolicy.ValidateOpenSlot(view, itemDefinition, liveSlot) !=
                PshopPurchasePolicy.OpenSlotOutcome.Success)
                return Abort(state.CharacterId, $"slot {page}/{slot} failed live-inventory validation");

            if (isProxy)
                offlineItems.Add(new OfflineShopItemSlotTvp((short)(page * PshopPurchasePolicy.MaxSlots + slot),
                    view.ItemId, view.Quantity, view.Value, view.Serial, view.Price, null));
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

    private static int CountOccupiedSlots(PshopInfo listing)
    {
        var count = 0;
        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
            if (PshopPurchasePolicy.ReadSlot(listing, page, slot).IsOccupied)
                count++;
        return count;
    }

    public async ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken)
    {
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        // Captured per accepted slot for the GL_1000_PXSHOP_REG audit row below -- must be read from the
        // LIVE inventory stack (not the wire-submitted PshopInfo slot, which carries no SocketGem1-3 fields
        // at all) before that slot is removed from page0/page1.
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

        var settings = await gameSettings.GetAsync(cancellationToken);
        var shopDate = GameDate.Today() + settings.ProxyShopDurationDays;

        try
        {
            await offlineShops.OpenAndReplaceContainersAsync(characterId, zone.MapId, shopDate, packet.PshopInfo.Name,
                (int)state.PosX, (int)state.PosY, (int)state.PosZ, offlineItems,
                ToTvps(page0), ToTvps(page1), cancellationToken);
        }
        catch (Exception ex)
        {
            // Known overlap, not a bug: this 102 means "persisting THIS proxy-open itself failed" (the stored
            // procedure's own CAS guard rejected an existing open/unclaimed shop row), a different cause from
            // PrepareAsync's Blocked(102) ("a PERSONAL request found an already-active proxy shop"). Legacy's
            // client-facing result-code space has no room for a fourth distinct code here, so both causes
            // surface identically as 102 to the client -- this is an accepted wire-level ambiguity, not
            // something Fenrir can resolve without inventing an unverified new code.
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop open OpenAndReplaceContainersAsync failed (treated as already open)",
                characterId);
            return new OpenShopStallResponse { Result = 102, PshopInfo = listing };
        }

        // Logged only once OpenAndReplaceContainersAsync above has durably committed -- one row per accepted
        // slot, matching legacy's own per-slot GL_1000_PXSHOP_REG call (Server/ts25zone/S07_MyGame09.cpp:491).
        foreach (var entry in auditEntries)
            await eventLog.LogAsync(ProxyShopListEventCode, EventLogCategory.ProxyShop, accountId, characterId,
                null, null, null, null, null, entry.ItemId, entry.Quantity, 1,
                $"Value={entry.Value};Serial={entry.Serial};Price={entry.Price};Socket1={entry.Socket1};Socket2={entry.Socket2};Socket3={entry.Socket3}",
                cancellationToken);

        logger.LogInformation(
            "Proxy shop opened: character {CharacterId} name {ShopName} rental until {ShopDate} ({ItemCount} items moved out of inventory)",
            characterId, listing.Name, shopDate, auditEntries.Count);

        var response = new OpenShopStallResponse { Result = 0, PshopInfo = listing };

        // Zone.RebroadcastProxyShops's periodic-broadcast table entry -- independent of PlayerRuntimeState
        // since this shop must keep advertising after its owner disconnects (see
        // ProxyShopBroadcastEntry's remarks).
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

    private OpenShopStallPrepareResult Abort(int characterId, string reason)
    {
        logger.LogWarning(
            "Open shop stall rejected: character {CharacterId} session will be disconnected ({Reason})",
            characterId, reason);
        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);
    }

    /// <summary>
    ///     A cross-type "shop already open" outcome (result 101/102/103) -- no disconnect, no shop-state
    ///     mutation. See <see cref="OpenShopStallPrepareOutcome.Blocked" />.
    /// </summary>
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

    /// <summary>One GL_1000_PXSHOP_REG audit row's worth of data for a single accepted listing slot.</summary>
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
