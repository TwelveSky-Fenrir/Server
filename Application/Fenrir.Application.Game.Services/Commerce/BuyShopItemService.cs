using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
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
    /// <summary>
    ///     SQL error THROWn by usp_PshopPurchase_Execute.sql when crediting the price to the seller's money
    ///     would exceed the legacy money cap -- the personal-shop path's Result=5 soft failure.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:6953-7124 (personal-path overflow blocks outright, result
    ///     5, no fallback) ; Database/StoredProcedures/game/usp_PshopPurchase_Execute.sql:38-40 (SQL 50275).
    /// </remarks>
    private const int SellerMoneyCapExceededErrorNumber = 50275;

    /// <summary>
    ///     SQL error THROWn by usp_OfflineShop_ExecutePurchase.sql when the listing slot no longer matches
    ///     what <see cref="TryResolveProxySellerAsync" /> read a moment earlier (another buyer already
    ///     claimed it, or the shop closed in the interim) -- the proxy-shop path's Result=4 soft failure,
    ///     the same wire code the LIVE path's own pre-lock staleness re-check uses in <see cref="CommitAsync" />.
    /// </summary>
    /// <remarks>Database/StoredProcedures/game/usp_OfflineShop_ExecutePurchase.sql:42-43 (SQL 50272).</remarks>
    private const int ProxyListingStaleErrorNumber = 50272;

    /// <summary>
    ///     SQL error THROWn by usp_OfflineShop_ExecutePurchase.sql when crediting the price would exceed the
    ///     proxy shop's own BigMoney cap (999) even after the automatic Money-overflow-into-BigMoney rollover
    ///     -- the proxy-shop path's Result=5 soft failure, the overflow-handling asymmetry against the LIVE
    ///     path's own hard Result=5 block (see that path's <see cref="SellerMoneyCapExceededErrorNumber" />).
    /// </summary>
    /// <remarks>Database/StoredProcedures/game/usp_OfflineShop_ExecutePurchase.sql:76-93 (SQL 50273).</remarks>
    private const int ProxyBigMoneyCapExceededErrorNumber = 50273;

    /// <summary>
    ///     game.EventLog.EventCode for a proxy-shop purchase row (legacy <c>GL_1001_PXSHOP_ITEM</c>, action
    ///     label "Purchased"), scoped within <see cref="EventLogCategory.ProxyShop" /> -- see that enum
    ///     member's remarks for the full 1-4 numbering. Same numeric value as
    ///     <c>UpdateProxyShopService.ProxyShopPurchaseEventCode</c>, the sibling opcode-109 code path that
    ///     logs the same conceptual event through the same <see cref="IOfflineShopRepository.ExecutePurchaseAsync" />
    ///     call.
    /// </summary>
    private const short ProxyShopPurchaseEventCode = 3;

    public async ValueTask<BuyShopItemSellerResult> FindSellerAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int buyerId, CancellationToken cancellationToken)
    {
        // Server/ts25zone/S04_MyWork02.cpp:6929-6939 -- the buyer's destination-slot coordinates are checked
        // BEFORE the wider town-server gate below, exactly matching the legacy ordering.
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

        // Server/ts25zone/S04_MyWork02.cpp:6925-6957 -- the dispatch always attempts the OFFLINE/proxy shop
        // path first (only reachable at all when the hosting zone is the single proxy-shop hub AND a
        // matching OPEN proxy shop is registered under the given seller name); any other outcome here (wrong
        // zone, no such character, shop never opened/currently closed) falls straight through to the
        // LIVE/personal-shop lookup below with no reply of its own, mirroring the legacy's single-condition
        // goto-fallthrough dispatch structure.
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
        // Re-validate against the SELLER's LIVE inventory now both locks are held -- the cached PshopListing
        // snapshot is only a display copy.
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
            // usp_PshopPurchase_Execute.sql THROWs 50275 specifically when crediting the price to the
            // SELLER's money would exceed the legacy money cap. Contract classifies this as a soft failure
            // (Result=5, connection stays alive) -- not the generic "treat as malformed/cheating" disconnect
            // below, which is reserved for the buyer-insufficient-funds case (SQL 50222) that the legacy
            // client's own UI is expected to prevent from ever being reachable.
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

        // B_BUY_PSHOP_RECV(6) "your item sold" -- seller's own source slot coordinates, same item
        // value/socket details as the buyer's Result=0 notification above (Server/ts25zone/S04_MyWork02.cpp:7067-7071).
        var sellerSoldNotification = BuildReply(6, slot.Price, packet.Page1, packet.Index1, newStack);

        // Awaited (not fire-and-forget) so the listing snapshot read below observes the POST-clear state --
        // the zone tick clears the sold slot and delivers the seller's own notifications as part of applying
        // this command (Zone.ApplyPshopCommand).
        await zone.PostPshopCommandAndWaitAsync(
            new PshopZoneCommand(seller.CharacterId, packet.Page1, packet.Index1, !stillHasItems,
                sellerSoldNotification),
            cancellationToken);

        logger.LogInformation(
            "PShop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from seller {SellerId} for {Price} (seller shop {SellerShopState})",
            buyer.CharacterId, slot.ItemId, slot.Quantity, seller.CharacterId, slot.Price,
            stillHasItems ? "still has items" : "now empty/closed");

        // B_DEMAND_PSHOP_RECV(0) buyer-facing listing refresh (Server/ts25zone/S04_MyWork02.cpp:7096-7100) --
        // the seller's own Result=3 counterpart is sent directly by Zone.ApplyPshopCommand.
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
            // Buyer-insufficient-funds (SQL 50222) and anything else collapse to the same disconnect
            // treatment as the LIVE path's own equivalent catch-all above -- the legacy client's own UI is
            // expected to prevent this from ever being reachable through normal play.
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

        // Logged only once ExecutePurchaseAsync above has durably committed -- ShopMoneyAfter/BigMoneyAfter
        // re-read fresh from the seller's shop row, same posture as UpdateProxyShopService.PurchaseAsync's
        // own audit write for the equivalent BuySort=2 purchase. TargetAccountId is left null: no cheap
        // characterId->accountId lookup exists on ICharacterRepository today, and the seller may be offline
        // (the whole point of a proxy shop) so no live PlayerRuntimeState is available either --
        // TargetCharacterId is still populated, and game.Characters.AccountId is trivially joinable from it.
        var (shopAfterPurchase, _) = await offlineShops.GetByCharacterAsync(sellerId, cancellationToken);
        await eventLog.LogAsync(ProxyShopPurchaseEventCode, EventLogCategory.ProxyShop, accountId, buyer.CharacterId,
            null, sellerId, null, slot.Price, null, slot.ItemId, slot.Quantity, 1,
            $"Action=Purchased;Value={slot.Value};Serial={slot.Serial};ShopOwnerName={packet.AvatarName};" +
            $"ShopMoneyAfter={shopAfterPurchase?.Money ?? 0};ShopBigMoneyAfter={shopAfterPurchase?.BigMoney ?? 0}",
            cancellationToken);

        logger.LogInformation(
            "Proxy PShop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from proxy seller {SellerId} for {Price}",
            buyer.CharacterId, slot.ItemId, slot.Quantity, sellerId, slot.Price);

        // Unlike the LIVE path, a proxy-shop sellout auto-close (usp_OfflineShop_ExecutePurchase's own
        // ShopState 1->0 update) has no live PshopZoneCommand/broadcast counterpart to post here -- the
        // proxy shop's state lives purely in SQL with no PlayerRuntimeState cache to refresh, matching
        // UpdateProxyShopService.PurchaseAsync's identical scope for the equivalent BuySort=2 purchase.
        return new BuyShopItemCommitResult(false, response, null);
    }

    /// <summary>
    ///     The OFFLINE/proxy-shop half of <see cref="FindSellerAsync" />'s dispatch. Returns <see langword="null" />
    ///     when the entry gate itself fails -- no character by that name, or none with a currently-open
    ///     (ShopState=1) proxy shop -- so the caller falls through to the LIVE/personal lookup with no reply
    ///     of its own. Once past that entry gate, every further rejection (self-purchase, out-of-range/empty
    ///     slot, unresolvable item) IS surfaced here directly rather than falling through, mirroring the
    ///     legacy's single-entry-condition goto structure (Server/ts25zone/S04_MyWork02.cpp:6925-6957).
    /// </summary>
    private async ValueTask<BuyShopItemSellerResult?> TryResolveProxySellerAsync(BuyShopItemRequest packet,
        int buyerId, CancellationToken cancellationToken)
    {
        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
            return null;

        var (shop, items) = await offlineShops.GetByCharacterAsync(sellerId.Value, cancellationToken);
        if (shop is not { ShopState: 1 })
            return null;

        // Same conservative rejection OfflineShop purchases already apply for the equivalent BuySort=2 path
        // (UpdateProxyShopService.PurchaseAsync) -- buying from one's own open shop would bypass the normal
        // "must be closed" retrieval gate and refund the price into the shop's own earnings.
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

        // InventoryPage/InventoryIndex/PosX/PosY are unused by ResolvePurchase/CommitProxyPurchaseAsync --
        // there is no live seller-side inventory container for a proxy shop to remove from, that half of the
        // mutation is the DELETE FROM game.OfflineShopItems inside ExecutePurchaseAsync's own SQL transaction.
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
