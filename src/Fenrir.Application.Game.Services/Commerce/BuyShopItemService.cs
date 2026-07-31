using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Domain.Game.GameData;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class BuyShopItemService(
    ICharacterRepository characters,
    IOfflineShopRepository offlineShops,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    ILogger<BuyShopItemService> logger) : IBuyShopItemService
{
    private const int SellerMoneyCapExceededErrorNumber = 50275;

    private const int ProxyListingStaleErrorNumber = 50272;

    private const int ProxyBigMoneyCapExceededErrorNumber = 50273;

    private const short ProxyShopPurchaseEventCode = 3;

    public async ValueTask<BuyShopItemSellerResult> FindSellerAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int buyerId, CancellationToken cancellationToken)
    {
        if (packet.XPost2 is < 0 or > 7 || packet.YPost2 is < 0 or > 7)
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId} sent out-of-range destination coordinates -- session will be disconnected",
                buyerId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId} is not in a town zone (zone {MapId}) -- session will be disconnected",
                buyerId, zone.MapId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        if (ProxyShopZonePolicy.IsProxyShopZone(zone.MapId))
        {
            var proxyResult = await TryResolveProxySellerAsync(packet, buyerId, cancellationToken);
            if (proxyResult is { } resolved)
                return resolved;
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
            logger.LogDebug("Buy shop item rejected: buyer {BuyerId} seller {SellerAvatarName} not found in zone",
                buyerId, packet.AvatarName);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(1, 0, 0, 0), null, default);
        }

        if (!seller.PshopOpen || seller.PshopListing is not { } listingSnapshot)
        {
            logger.LogDebug("Buy shop item rejected: seller {SellerId} has no personal shop open", seller.CharacterId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(2, 0, 0, 0), null, default);
        }

        if (listingSnapshot.UniqueNumber != packet.UniqueNumber)
        {
            logger.LogDebug(
                "Buy shop item rejected: buyer {BuyerId} stale listing (expected {ExpectedUniqueNumber}, got {ActualUniqueNumber})",
                buyerId, listingSnapshot.UniqueNumber, packet.UniqueNumber);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(7, 0, 0, 0), null, default);
        }

        if (packet.Page1 is < 0 || packet.Page1 >= PshopPurchasePolicy.MaxPages ||
            packet.Index1 is < 0 || packet.Index1 >= PshopPurchasePolicy.MaxSlots)
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId} sent out-of-range seller slot {Page1}/{Index1} -- session will be disconnected",
                buyerId, packet.Page1, packet.Index1);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        var slot = PshopPurchasePolicy.ReadSlot(listingSnapshot, packet.Page1, packet.Index1);
        if (!slot.IsOccupied)
        {
            logger.LogDebug("Buy shop item rejected: seller {SellerId} slot {Page1}/{Index1} is empty",
                seller.CharacterId, packet.Page1, packet.Index1);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(3, 0, 0, 0), null, default);
        }

        if (slot.InventoryPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)slot.InventoryPage, slot.InventoryIndex))
        {
            logger.LogDebug(
                "Buy shop item rejected: seller {SellerId} listing slot {Page1}/{Index1} maps to an invalid inventory coordinate",
                seller.CharacterId, packet.Page1, packet.Index1);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(3, 0, 0, 0), null, default);
        }

        if (buyerId == seller.CharacterId)
        {
            logger.LogWarning(
                "Buy shop item rejected: character {CharacterId} attempted to buy from its own shop -- session will be disconnected",
                buyerId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Proceed, null, seller, slot);
    }

    public async ValueTask<BuyShopItemCommitResult> CommitAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, PlayerRuntimeState seller, PshopPurchasePolicy.SlotView slot,
        CancellationToken cancellationToken)
    {
        var liveSellerStack = seller.Inventory.GetSlot((byte)slot.InventoryPage, (byte)slot.InventoryIndex);
        if (liveSellerStack is not { } liveStack || liveStack.ItemId != slot.ItemId ||
            liveStack.Quantity != slot.Quantity || liveStack.Value() != slot.Value)
        {
            logger.LogInformation(
                "Buy shop item rejected: buyer {BuyerId}/seller {SellerId} slot {Page1}/{Index1} changed since it was listed (stale purchase)",
                buyer.CharacterId, seller.CharacterId, packet.Page1, packet.Index1);
            return new BuyShopItemCommitResult(false, BuildReply(4, 0, 0, 0), null);
        }

        if (!worldData.ItemsById.TryGetValue(slot.ItemId, out var itemDefinition))
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId}/seller {SellerId} item {ItemId} is unresolvable in the world data catalog",
                buyer.CharacterId, seller.CharacterId, slot.ItemId);
            return new BuyShopItemCommitResult(true, null, null);
        }

        var buyerDestination = buyer.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var resolved = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, buyerDestination);

        if (!resolved.Succeeded)
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId}/seller {SellerId} purchase resolution failed (destination slot incompatible) -- session will be disconnected",
                buyer.CharacterId, seller.CharacterId);
            return new BuyShopItemCommitResult(true, null, null);
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
        catch (SqlException ex) when (ex.Number == SellerMoneyCapExceededErrorNumber)
        {
            logger.LogInformation(
                "PShop purchase blocked: crediting {Price} to seller {SellerId} would exceed the maximum money value (buyer {BuyerId})",
                slot.Price, seller.CharacterId, buyer.CharacterId);
            return new BuyShopItemCommitResult(false, BuildReply(5, 0, 0, 0), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PShop purchase ExecutePshopPurchaseAsync failed for buyer {BuyerId}/seller {SellerId} (treated as insufficient/over-cap)",
                buyer.CharacterId, seller.CharacterId);
            return new BuyShopItemCommitResult(true, null, null);
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

        var sellerSoldNotification = BuildReply(6, slot.Price, packet.Page1, packet.Index1, newStack);

        await zone.PostPshopCommandAndWaitAsync(
            new PshopZoneCommand(seller.CharacterId, packet.Page1, packet.Index1, !stillHasItems,
                sellerSoldNotification),
            cancellationToken);

        logger.LogInformation(
            "PShop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from seller {SellerId} for {Price} (seller shop {SellerShopState})",
            buyer.CharacterId, slot.ItemId, slot.Quantity, seller.CharacterId, slot.Price,
            stillHasItems ? "still has items" : "now empty/closed");

        var listingRefresh = new ViewShopStallResponse { Result = 0, PshopInfo = seller.PshopListing!.Value };

        return new BuyShopItemCommitResult(false, response, listingRefresh);
    }

    public async ValueTask<BuyShopItemCommitResult> CommitProxyPurchaseAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int sellerId, int accountId, PshopPurchasePolicy.SlotView slot,
        CancellationToken cancellationToken)
    {
        if (!worldData.ItemsById.TryGetValue(slot.ItemId, out var itemDefinition))
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId}/proxy seller {SellerId} item {ItemId} is unresolvable in the world data catalog",
                buyer.CharacterId, sellerId, slot.ItemId);
            return new BuyShopItemCommitResult(true, null, null);
        }

        var buyerDestination = buyer.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var resolved = PshopPurchasePolicy.ResolvePurchase(slot, itemDefinition, buyerDestination);

        if (!resolved.Succeeded)
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId}/proxy seller {SellerId} purchase resolution failed (destination slot incompatible) -- session will be disconnected",
                buyer.CharacterId, sellerId);
            return new BuyShopItemCommitResult(true, null, null);
        }

        var slotIndex = (short)(packet.Page1 * PshopPurchasePolicy.MaxSlots + packet.Index1);
        var projectedBuyerContainer = buyer.Inventory.GetContainer((byte)packet.Page2)
            .SetItem((byte)packet.Index2, resolved.NewDestinationStack!.Value);

        try
        {
            await offlineShops.ExecutePurchaseAsync(sellerId, slotIndex, slot.ItemId, slot.Quantity, slot.Value,
                slot.Price, buyer.CharacterId, (byte)packet.Page2, ToTvps(projectedBuyerContainer),
                cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == ProxyListingStaleErrorNumber)
        {
            logger.LogInformation(
                "Proxy PShop purchase rejected: proxy seller {SellerId} slot {Page1}/{Index1} changed since it was listed (stale purchase, buyer {BuyerId})",
                sellerId, packet.Page1, packet.Index1, buyer.CharacterId);
            return new BuyShopItemCommitResult(false, BuildReply(4, 0, 0, 0), null);
        }
        catch (SqlException ex) when (ex.Number == ProxyBigMoneyCapExceededErrorNumber)
        {
            logger.LogInformation(
                "Proxy PShop purchase blocked: crediting {Price} to proxy seller {SellerId} would exceed the BigMoney cap (buyer {BuyerId})",
                slot.Price, sellerId, buyer.CharacterId);
            return new BuyShopItemCommitResult(false, BuildReply(5, 0, 0, 0), null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Proxy PShop purchase ExecutePurchaseAsync failed for buyer {BuyerId}/proxy seller {SellerId} (treated as insufficient funds)",
                buyer.CharacterId, sellerId);
            return new BuyShopItemCommitResult(true, null, null);
        }

        var newStack = resolved.NewDestinationStack!.Value;
        var response = BuildReply(0, slot.Price, packet.Page2, packet.Index2, newStack);

        var buyerContainers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.Page2, projectedBuyerContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(buyer.CharacterId, buyerContainers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped proxy PShop-buy buyer mirror for character {CharacterId}",
                zone.MapId, buyer.CharacterId);

        var (shopAfterPurchase, _) = await offlineShops.GetByCharacterAsync(sellerId, cancellationToken);
        await eventLog.LogAsync(ProxyShopPurchaseEventCode, EventLogCategory.ProxyShop, accountId, buyer.CharacterId,
            null, sellerId, null, slot.Price, null, slot.ItemId, slot.Quantity, 1,
            $"Action=Purchased;Value={slot.Value};Serial={slot.Serial};ShopOwnerName={packet.AvatarName};" +
            $"ShopMoneyAfter={shopAfterPurchase?.Money ?? 0};ShopBigMoneyAfter={shopAfterPurchase?.BigMoney ?? 0}",
            cancellationToken);

        logger.LogInformation(
            "Proxy PShop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from proxy seller {SellerId} for {Price}",
            buyer.CharacterId, slot.ItemId, slot.Quantity, sellerId, slot.Price);

        return new BuyShopItemCommitResult(false, response, null);
    }

    private async ValueTask<BuyShopItemSellerResult?> TryResolveProxySellerAsync(BuyShopItemRequest packet,
        int buyerId, CancellationToken cancellationToken)
    {
        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
            return null;

        var (shop, items) = await offlineShops.GetByCharacterAsync(sellerId.Value, cancellationToken);
        if (shop is not { ShopState: 1 })
            return null;

        if (sellerId.Value == buyerId)
        {
            logger.LogWarning(
                "Buy shop item rejected: character {CharacterId} attempted to buy from its own proxy shop -- session will be disconnected",
                buyerId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        if (packet.Page1 is < 0 || packet.Page1 >= PshopPurchasePolicy.MaxPages ||
            packet.Index1 is < 0 || packet.Index1 >= PshopPurchasePolicy.MaxSlots)
        {
            logger.LogWarning(
                "Buy shop item rejected: buyer {BuyerId} sent out-of-range proxy-shop slot {Page1}/{Index1} -- session will be disconnected",
                buyerId, packet.Page1, packet.Index1);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        var slotIndex = (short)(packet.Page1 * PshopPurchasePolicy.MaxSlots + packet.Index1);
        var item = items.FirstOrDefault(row => row.SlotIndex == slotIndex);
        if (item is null || item.ItemId is not { } itemId)
        {
            logger.LogDebug("Buy shop item rejected: proxy seller {SellerId} slot {Page1}/{Index1} is empty",
                sellerId.Value, packet.Page1, packet.Index1);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Reply, BuildReply(3, 0, 0, 0), null, default);
        }

        if (!worldData.ItemsById.ContainsKey(itemId))
        {
            logger.LogWarning(
                "Buy shop item rejected: proxy seller {SellerId} item {ItemId} is unresolvable in the world data catalog -- session will be disconnected",
                sellerId.Value, itemId);
            return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.Abort, null, null, default);
        }

        var slot = new PshopPurchasePolicy.SlotView(itemId, item.Quantity, item.Value, item.SerialNumber,
            item.Price, 0, 0, 0, 0);
        return new BuyShopItemSellerResult(BuyShopItemSellerOutcome.ProxyProceed, null, null, slot, sellerId.Value);
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
