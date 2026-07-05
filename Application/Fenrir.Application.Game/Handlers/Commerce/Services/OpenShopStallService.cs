using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Pshop;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>The three ways <see cref="OpenShopStallService.Prepare" /> can leave <see cref="OpenShopStallHandler" />.</summary>
public enum OpenShopStallPrepareOutcome
{
    /// <summary>A protocol-level fault -- the handler should abort the session.</summary>
    Abort,

    /// <summary>The live personal shop was opened synchronously; the handler should send <see cref="OpenShopStallPrepareResult.LiveResponse" />.</summary>
    LiveOpened,

    /// <summary>The offline/deputy shop's slots validated; the handler should acquire the economy lock and call <see cref="IOpenShopStallService.OpenProxyShopAsync" />.</summary>
    ProxyReady
}

public readonly record struct OpenShopStallPrepareResult(
    OpenShopStallPrepareOutcome Outcome,
    OpenShopStallResponse? LiveResponse,
    PshopInfo Listing,
    List<OfflineShopItemSlotTvp>? OfflineItems);

/// <summary>Business logic for CZ_START_PSHOP_SEND (opcode 31), extracted from <see cref="OpenShopStallHandler" />.</summary>
public interface IOpenShopStallService
{
    /// <summary>
    ///     Validates the submitted stall and either opens the LIVE shop synchronously (a pure display overlay --
    ///     items never leave inventory) or, for the offline/deputy shop, validates every occupied slot against
    ///     the live inventory and hands back the data <see cref="OpenProxyShopAsync" /> needs.
    /// </summary>
    OpenShopStallPrepareResult Prepare(OpenShopStallRequest packet, PlayerRuntimeState state);

    /// <summary>
    ///     Physically removes the advertised items from inventory into the offline shop and persists it. The
    ///     caller's <see cref="PlayerRuntimeState.EconomyActionLock" /> must already be held.
    /// </summary>
    ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, PshopInfo listing, List<OfflineShopItemSlotTvp> offlineItems,
        CancellationToken cancellationToken);
}

/// <remarks>
///     The legacy's rental expiration column has no Fenrir equivalent -- a Fenrir-invented window is used
///     instead, admin-tunable via <c>ProxyShopDurationDays</c>.
/// </remarks>
public sealed class OpenShopStallService(
    IOfflineShopRepository offlineShops,
    IGameSettingsRepository gameSettings,
    WorldDataCache worldData,
    ILogger<OpenShopStallService> logger) : IOpenShopStallService
{
    public OpenShopStallPrepareResult Prepare(OpenShopStallRequest packet, PlayerRuntimeState state)
    {
        if (packet.Sort is not (1 or 2))
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

        var isProxy = packet.Sort == 2;

        if (!isProxy && state.PshopOpen)
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

        if (string.IsNullOrWhiteSpace(packet.PshopInfo.Name))
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

        var anyOccupied = false;
        for (var page = 0; page < PshopPurchasePolicy.MaxPages && !anyOccupied; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
            if (PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot).IsOccupied)
            {
                anyOccupied = true;
                break;
            }

        if (!anyOccupied)
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

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
                return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

            worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition);
            var liveSlot = state.Inventory.GetSlot((byte)view.InventoryPage, (byte)view.InventoryIndex);

            if (PshopPurchasePolicy.ValidateOpenSlot(view, itemDefinition, liveSlot) !=
                PshopPurchasePolicy.OpenSlotOutcome.Success)
                return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.Abort, null, default, null);

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
            return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.LiveOpened,
                new OpenShopStallResponse { Result = 0, PshopInfo = listing }, listing, null);
        }

        return new OpenShopStallPrepareResult(OpenShopStallPrepareOutcome.ProxyReady, null, listing, offlineItems);
    }

    public async ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, PshopInfo listing, List<OfflineShopItemSlotTvp> offlineItems,
        CancellationToken cancellationToken)
    {
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
        {
            var view = PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot);
            if (!view.IsOccupied)
                continue;

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
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop open OpenAndReplaceContainersAsync failed (treated as already open)",
                characterId);
            return new OpenShopStallResponse { Result = 102, PshopInfo = listing };
        }

        var response = new OpenShopStallResponse { Result = 0, PshopInfo = listing };

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

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
