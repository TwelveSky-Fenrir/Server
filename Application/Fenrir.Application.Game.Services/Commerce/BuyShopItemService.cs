using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class BuyShopItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<BuyShopItemService> logger) : IBuyShopItemService
{
    public BuyShopItemSellerResult FindSeller(BuyShopItemRequest packet, Zone zone, PlayerRuntimeState buyer,
        int buyerId)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);

        if (packet.XPost2 is < 0 or > 7 || packet.YPost2 is < 0 or > 7)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);

        PlayerRuntimeState? seller = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                seller = candidate;
                break;
            }

        if (seller is null)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(1, 0, 0, 0), null, default);

        if (!seller.PshopOpen || seller.PshopListing is not { } listingSnapshot)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(2, 0, 0, 0), null, default);

        if (listingSnapshot.UniqueNumber != packet.UniqueNumber)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(7, 0, 0, 0), null, default);

        if (packet.Page1 is < 0 || packet.Page1 >= PshopPurchasePolicy.MaxPages ||
            packet.Index1 is < 0 || packet.Index1 >= PshopPurchasePolicy.MaxSlots)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);

        var slot = PshopPurchasePolicy.ReadSlot(listingSnapshot, packet.Page1, packet.Index1);
        if (!slot.IsOccupied)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(3, 0, 0, 0), null, default);

        if (slot.InventoryPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)slot.InventoryPage, slot.InventoryIndex))
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(3, 0, 0, 0), null, default);

        if (buyerId == seller.CharacterId)
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);

        return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Proceed, null, seller, slot);
    }

    public async ValueTask<BuyShopItemCommitResult> CommitAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, PlayerRuntimeState seller, PshopPurchasePolicy.SlotView slot,
        CancellationToken cancellationToken)
    {
        // Re-validate against the SELLER's LIVE inventory now both locks are held -- the cached PshopListing
        // snapshot is only a display copy.
        var liveSellerStack = seller.Inventory.GetSlot((byte)slot.InventoryPage, (byte)slot.InventoryIndex);
        if (liveSellerStack is not { } liveStack || liveStack.ItemId != slot.ItemId ||
            liveStack.Quantity != slot.Quantity || liveStack.Value() != slot.Value)
            return new BuyShopItemCommitResult(false, BuildReply(4, 0, 0, 0), false);

        if (!worldData.ItemsById.TryGetValue(slot.ItemId, out var itemDefinition))
            return new BuyShopItemCommitResult(true, null, false);

        var buyerDestination = buyer.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var resolved = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, buyerDestination);

        if (!resolved.Succeeded)
            return new BuyShopItemCommitResult(true, null, false);

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
            return new BuyShopItemCommitResult(true, null, false);
        }

        var newStack = resolved.NewDestinationStack!.Value;
        var response = BuildReply(0, slot.Price, packet.Page2, packet.Index2, newStack);

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
        return new BuyShopItemCommitResult(false, response, !stillHasItems);
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

    private static BuyShopItemResponse BuildReply(int result, int cost, int page, int index, ItemStack? stack = null)
    {
        var value = stack is { } s ? [s.ItemId, 0, 0, s.Quantity, s.Value(), s.Serial] : new int[6];
        var socket = stack is { } s2 ? [s2.SocketGem1, s2.SocketGem2, s2.SocketGem3] : new int[3];

        return new BuyShopItemResponse
        {
            Result = result, Cost = cost, Page = page, Index = index, Value = value, Socket = socket
        };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
