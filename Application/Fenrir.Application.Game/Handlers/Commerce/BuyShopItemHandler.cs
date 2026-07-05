using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Social.Pshop;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_PSHOP_SEND (opcode 35) -- purchase from a LIVE personal-shop stall. Two-character economy
///     action: both participants' <see cref="PlayerRuntimeState.EconomyActionLock" /> are held, smaller
///     CharacterId first to avoid deadlock. Gated by <see cref="NpcShopPolicy.TownZoneNumbers" />,
///     deliberately asymmetric with the other PShop opcodes' zone-37-only gate.
/// </summary>
public sealed class BuyShopItemHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<BuyShopItemHandler> logger)
    : IAsyncPacketHandler<BuyShopItemRequest>
{
    public async ValueTask HandleAsync(BuyShopItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var buyerId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(buyerId, out var buyer) || buyer is null)
            return;

        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.XPost2 is < 0 or > 7 || packet.YPost2 is < 0 or > 7)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? seller = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                seller = candidate;
                break;
            }

        if (seller is null)
        {
            Reply(session, 1, 0, 0, 0);
            return;
        }

        if (!seller.PshopOpen || seller.PshopListing is not { } listingSnapshot)
        {
            Reply(session, 2, 0, 0, 0);
            return;
        }

        if (listingSnapshot.UniqueNumber != packet.UniqueNumber)
        {
            Reply(session, 7, 0, 0, 0);
            return;
        }

        if (packet.Page1 is < 0 || packet.Page1 >= PshopPurchasePolicy.MaxPages ||
            packet.Index1 is < 0 || packet.Index1 >= PshopPurchasePolicy.MaxSlots)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slot = PshopPurchasePolicy.ReadSlot(listingSnapshot, packet.Page1, packet.Index1);
        if (!slot.IsOccupied)
        {
            Reply(session, 3, 0, 0, 0);
            return;
        }

        if (slot.InventoryPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)slot.InventoryPage, slot.InventoryIndex))
        {
            Reply(session, 3, 0, 0, 0);
            return;
        }

        if (buyerId == seller.CharacterId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var (first, second) = buyer.CharacterId < seller.CharacterId ? (buyer, seller) : (seller, buyer);

        await first.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await second.EconomyActionLock.WaitAsync(cancellationToken);
            try
            {
                await CommitAsync(packet, session, zoneSession, zone, buyer, seller, slot, cancellationToken);
            }
            finally
            {
                second.EconomyActionLock.Release();
            }
        }
        finally
        {
            first.EconomyActionLock.Release();
        }
    }

    private async ValueTask CommitAsync(BuyShopItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState buyer, PlayerRuntimeState seller,
        PshopPurchasePolicy.SlotView slot, CancellationToken cancellationToken)
    {
        // Re-validate against the SELLER's LIVE inventory now both locks are held -- the cached PshopListing
        // snapshot is only a display copy.
        var liveSellerStack = seller.Inventory.GetSlot((byte)slot.InventoryPage, (byte)slot.InventoryIndex);
        if (liveSellerStack is not { } liveStack || liveStack.ItemId != slot.ItemId ||
            liveStack.Quantity != slot.Quantity || liveStack.Value() != slot.Value)
        {
            Reply(session, 4, 0, 0, 0);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(slot.ItemId, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var buyerDestination = buyer.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var resolved = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, buyerDestination);

        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var projectedSellerContainer = seller.Inventory.GetContainer((byte)slot.InventoryPage)
            .Remove((byte)slot.InventoryIndex);
        var projectedBuyerContainer = buyer.Inventory.GetContainer((byte)packet.Page2)
            .SetItem((byte)packet.Index2, resolved.NewDestinationStack!.Value);

        try
        {
            await characters.ExecutePshopPurchaseAsync(seller.CharacterId, (byte)slot.InventoryPage,
                ToTvps(projectedSellerContainer), buyer.CharacterId, (byte)packet.Page2,
                ToTvps(projectedBuyerContainer), slot.Price, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PShop purchase ExecutePshopPurchaseAsync failed for buyer {BuyerId}/seller {SellerId} (treated as insufficient/over-cap)",
                buyer.CharacterId, seller.CharacterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newStack = resolved.NewDestinationStack!.Value;
        Reply(session, 0, slot.Price, packet.Page2, packet.Index2, newStack);

        var sellerContainers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)slot.InventoryPage, projectedSellerContainer));
        var buyerContainers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.Page2, projectedBuyerContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(buyer.CharacterId, buyerContainers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped PShop-buy buyer mirror for character {CharacterId}",
                zone.MapId, buyer.CharacterId);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(seller.CharacterId, sellerContainers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped PShop-buy seller mirror for character {CharacterId}",
                zone.MapId, seller.CharacterId);

        var stillHasItems = HasAnyOtherOccupiedSlot(seller.PshopListing, packet.Page1, packet.Index1);
        zone.PostPshopCommand(new PshopZoneCommand(seller.CharacterId, packet.Page1, packet.Index1,
            !stillHasItems));

        // Reaches only the buyer (same connection); the seller's own close mirror rides PshopZoneCommand above.
        if (!stillHasItems)
            session.Send(new CloseShopStallResponse { Result = 1 });
    }

    private static bool HasAnyOtherOccupiedSlot(PshopInfo? listing, int soldPage, int soldSlot)
    {
        if (listing is not { } info)
            return false;

        for (var page = 0; page < PshopPurchasePolicy.MaxPages; page++)
        for (var s = 0; s < PshopPurchasePolicy.MaxSlots; s++)
        {
            if (page == soldPage && s == soldSlot)
                continue;
            if (PshopPurchasePolicy.ReadSlot(info, page, s).IsOccupied)
                return true;
        }

        return false;
    }

    private static void Reply(IPacketSession session, int result, int cost, int page, int index,
        ItemStack? stack = null)
    {
        var value = stack is { } s ? [s.ItemId, 0, 0, s.Quantity, s.Value(), s.Serial] : new int[6];
        var socket = stack is { } s2 ? [s2.SocketGem1, s2.SocketGem2, s2.SocketGem3] : new int[3];

        session.Send(new BuyShopItemResponse
        {
            Result = result, Cost = cost, Page = page, Index = index, Value = value, Socket = socket
        });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
