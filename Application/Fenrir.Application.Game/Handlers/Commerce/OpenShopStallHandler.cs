using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Pshop;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_START_PSHOP_SEND (opcode 31, contracts/04_commerce.md, verified <c>S04_MyWork02.cpp:6021-6348</c>).
///     <c>Sort</c> 1 = live personal shop (a pure display overlay -- items never leave
///     <see cref="PlayerRuntimeState.Inventory" />), 2 = offline/deputy shop (items physically leave into
///     game.OfflineShopItems). Both sorts gated to zone 37 under <c>PPSHOP_V2</c> (verified, checked before
///     the sort switch).
/// </summary>
/// <remarks>
///     OPEN ISSUES (documented, not guessed): <c>CheckPossiblePShopRegion</c> (per-tribe/zone sub-region
///     gate) not modeled -- treated as "anywhere in zone 37" (a safe superset). <c>aAction.aSort != 1</c>
///     ("must be standing") not modeled -- its semantics conflict with
///     <see cref="PlayerRuntimeState.ActionSort" />'s established 0-idle convention and weren't verified,
///     so it's not enforced on a guess. <c>iCheckAvatarShop</c> (item barred from personal-shop sale) not
///     modeled -- no such field exists on <c>ItemRowDto</c> yet. The proxy shop's rental expiration
///     (<c>aProxyShopDate</c>) has no Fenrir column -- a Fenrir-invented window is used instead,
///     admin-tunable via <c>ProxyShopDurationDays</c> (<see cref="IGameSettingsRepository" />) since
///     there's no legacy value to stay iso to.
/// </remarks>
public sealed class OpenShopStallHandler(
    IOfflineShopRepository offlineShops,
    IGameSettingsRepository gameSettings,
    WorldDataCache worldData,
    ILogger<OpenShopStallHandler> logger)
    : IAsyncPacketHandler<OpenShopStallRequest>
{
    /// <summary>Zone 37 only, verified for BOTH sorts under PPSHOP_V2 (S04_MyWork02.cpp:6040).</summary>
    public const short PshopZoneNumber = 37;

    public async ValueTask HandleAsync(OpenShopStallRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort is not (1 or 2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var isProxy = packet.Sort == 2;

        if (!isProxy && state.PshopOpen)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (string.IsNullOrWhiteSpace(packet.PshopInfo.Name))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var anyOccupied = false;
        for (var page = 0; page < PshopPurchasePolicy.MaxPages && !anyOccupied; page++)
        for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots; slot++)
            if (PshopPurchasePolicy.ReadSlot(packet.PshopInfo, page, slot).IsOccupied)
            {
                anyOccupied = true;
                break;
            }

        if (!anyOccupied)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition);
            var liveSlot = state.Inventory.GetSlot((byte)view.InventoryPage, (byte)view.InventoryIndex);

            if (PshopPurchasePolicy.ValidateOpenSlot(view, itemDefinition, liveSlot) !=
                PshopPurchasePolicy.OpenSlotOutcome.Success)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (isProxy)
                offlineItems.Add(new OfflineShopItemSlotTvp((short)(page * PshopPurchasePolicy.MaxSlots + slot),
                    view.ItemId, view.Quantity, view.Value, view.Serial, view.Price, SocketData: null));
        }

        // Fresh server-assigned UniqueNumber (legacy: mGAME.mAvatarPShopUniqueNumber++); CharacterId-derived
        // is a documented stand-in, same posture as PlayerRuntimeState.UniqueNumber.
        var uniqueNumber = unchecked((uint)(characterId * 2 + (isProxy ? 1 : 0)));
        var listing = packet.PshopInfo with { UniqueNumber = uniqueNumber };

        if (!isProxy)
        {
            state.PshopOpen = true;
            state.PshopListing = listing;
            session.Send(new OpenShopStallResponse { Result = 0, PshopInfo = listing });
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await OpenProxyShopAsync(packet, session, zoneSession, zone, state, characterId, listing, offlineItems,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask OpenProxyShopAsync(OpenShopStallRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken)
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
        var shopDate = GameDate.Today() + settings.ProxyShopDurationDays; // not real calendar-add arithmetic -- shopDate is only ever compared for equality/staleness elsewhere, never added-to again.

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
            session.Send(new OpenShopStallResponse { Result = 102, PshopInfo = listing });
            return;
        }

        session.Send(new OpenShopStallResponse { Result = 0, PshopInfo = listing });

        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, page0),
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, page1));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop-open mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
