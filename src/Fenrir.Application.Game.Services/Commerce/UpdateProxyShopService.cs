using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class UpdateProxyShopService(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<UpdateProxyShopService> logger) : IUpdateProxyShopService
{
    private const short ProxyShopRetrieveEventCode = 2;

    private const short ProxyShopPurchaseEventCode = 3;

    public UpdateProxyShopValidation Validate(UpdateProxyShopRequest packet)
    {
        if (packet.BuySort is not (1 or 2))
        {
            logger.LogDebug("Update proxy shop validation failed: invalid buySort {BuySort}", packet.BuySort);
            return new UpdateProxyShopValidation(true, 0, null);
        }

        var slotIndex = (short)(packet.SellPage * 5 + packet.SellIndex);
        if (packet.SellPage is < 0 or >= 5 || packet.SellIndex is < 0 or >= 5 ||
            packet.SelfPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.SelfPage, packet.SelfIndex) ||
            packet.SelfX is < 0 or > 7 || packet.SelfY is < 0 or > 7)
        {
            logger.LogDebug("Update proxy shop validation failed: out-of-range slot coordinates");
            return new UpdateProxyShopValidation(true, 0, null);
        }

        if (!worldData.ItemsById.TryGetValue(packet.SellItemIndex, out var itemDefinition))
        {
            logger.LogDebug("Update proxy shop validation failed: item {ItemId} is unresolvable",
                packet.SellItemIndex);
            return new UpdateProxyShopValidation(true, 0, null);
        }

        return new UpdateProxyShopValidation(false, slotIndex, itemDefinition);
    }

    public async ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        if (IsExpiredDatedPage(packet, state))
        {
            logger.LogWarning(
                "Offline-shop retrieve rejected: character {CharacterId} targeted the expired dated last inventory page -- session will be disconnected",
                characterId);
            return null;
        }

        var (_, ownItems) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        if (FindListing(ownItems, slotIndex, packet.SellItemIndex) is not { } listing)
        {
            logger.LogInformation(
                "Offline-shop retrieve rejected: character {CharacterId} slot {SlotIndex} no longer matches the server-side listing",
                characterId, slotIndex);
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, null, 0);
        }

        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + listing.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            logger.LogWarning(
                "Offline-shop retrieve rejected: character {CharacterId} destination slot {SelfPage}/{SelfIndex} cannot accept item {ItemId} -- session will be disconnected",
                characterId, packet.SelfPage, packet.SelfIndex, packet.SellItemIndex);
            return null;
        }

        var finalQuantity = destination is { } d ? d.Quantity + listing.Quantity : listing.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(listing.Value);
        var (gem1, gem2, gem3) = PshopPurchasePolicy.DecodeSocketData(listing.SocketData);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            gem1, gem2, gem3, 0, listing.SerialNumber);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.RetrieveItemAndReplaceContainerAsync(characterId, slotIndex, packet.SellItemIndex,
                listing.Quantity, listing.Value, (byte)packet.SelfPage, ToTvps(projectedContainer),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop retrieve RetrieveItemAndReplaceContainerAsync failed",
                characterId);
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, newStack, 0);
        }

        var response = BuildReply(0, packet.SelfPage, packet.SelfIndex, newStack, 0);

        var (shopAfterRetrieve, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        await eventLog.LogAsync(ProxyShopRetrieveEventCode, EventLogCategory.ProxyShop, accountId, characterId,
            null, null, null, 0, null, packet.SellItemIndex, listing.Quantity, 1,
            $"Action=Retrieved;Value={listing.Value};Serial={listing.SerialNumber};Socket1={gem1};" +
            $"Socket2={gem2};Socket3={gem3};ShopOwnerName={state.Name};" +
            $"ShopMoneyAfter={shopAfterRetrieve?.Money ?? 0};ShopBigMoneyAfter={shopAfterRetrieve?.BigMoney ?? 0}",
            cancellationToken);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop retrieve mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Offline-shop item retrieved: character {CharacterId} retrieved item {ItemId} x{Quantity} from their own closed shop",
            characterId, packet.SellItemIndex, listing.Quantity);

        return response;
    }

    public async ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        if (IsExpiredDatedPage(packet, state))
        {
            logger.LogWarning(
                "Offline-shop purchase rejected: character {CharacterId} targeted the expired dated last inventory page -- session will be disconnected",
                characterId);
            return null;
        }

        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
        {
            logger.LogDebug(
                "Offline-shop purchase rejected: character {CharacterId} seller {SellerAvatarName} does not exist",
                characterId, packet.AvatarName);
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, null, 0);
        }

        if (sellerId.Value == characterId)
        {
            logger.LogWarning(
                "Offline-shop purchase rejected: character {CharacterId} attempted to buy from its own proxy shop -- session will be disconnected",
                characterId);
            return null;
        }

        var (_, sellerItems) = await offlineShops.GetByCharacterAsync(sellerId.Value, cancellationToken);
        if (FindListing(sellerItems, slotIndex, packet.SellItemIndex) is not { } listing)
        {
            logger.LogInformation(
                "Offline-shop purchase rejected: character {CharacterId} seller {SellerId} slot {SlotIndex} no longer matches the server-side listing",
                characterId, sellerId.Value, slotIndex);
            return BuildReply(2, packet.SelfPage, packet.SelfIndex, null, 0);
        }

        if (packet.Price != listing.Price)
        {
            logger.LogInformation(
                "Offline-shop purchase rejected: character {CharacterId} agreed price {ClientPrice} but listing is {ListingPrice}",
                characterId, packet.Price, listing.Price);
            return BuildReply(2, packet.SelfPage, packet.SelfIndex, null, 0);
        }

        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + listing.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            logger.LogWarning(
                "Offline-shop purchase rejected: character {CharacterId} destination slot {SelfPage}/{SelfIndex} cannot accept item {ItemId} -- session will be disconnected",
                characterId, packet.SelfPage, packet.SelfIndex, packet.SellItemIndex);
            return null;
        }

        var finalQuantity = destination is { } d ? d.Quantity + listing.Quantity : listing.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(listing.Value);
        var (gem1, gem2, gem3) = PshopPurchasePolicy.DecodeSocketData(listing.SocketData);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            gem1, gem2, gem3, 0, listing.SerialNumber);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.ExecutePurchaseAsync(sellerId.Value, slotIndex, packet.SellItemIndex, listing.Quantity,
                listing.Value, listing.Price, characterId, (byte)packet.SelfPage, ToTvps(projectedContainer),
                cancellationToken);
        }
        catch (Exception ex) when (ProxyShopDeputyFailureClassifier.IsStaleListingFailure(ex))
        {
            logger.LogInformation(ex,
                "Offline-shop purchase rejected: character {CharacterId} proxy listing changed since it was read (stale purchase) -- session will be disconnected",
                characterId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} offline-shop purchase ExecutePurchaseAsync failed",
                characterId);
            return BuildReply(2, packet.SelfPage, packet.SelfIndex, null, 0);
        }

        var response = BuildReply(1000, packet.SelfPage, packet.SelfIndex, newStack, listing.Price);

        var (shopAfterPurchase, _) = await offlineShops.GetByCharacterAsync(sellerId.Value, cancellationToken);
        await eventLog.LogAsync(ProxyShopPurchaseEventCode, EventLogCategory.ProxyShop, accountId, characterId,
            null, sellerId.Value, null, listing.Price, null, packet.SellItemIndex, listing.Quantity, 1,
            $"Action=Purchased;Value={listing.Value};Serial={listing.SerialNumber};Socket1={gem1};" +
            $"Socket2={gem2};Socket3={gem3};ShopOwnerName={packet.AvatarName};" +
            $"ShopMoneyAfter={shopAfterPurchase?.Money ?? 0};ShopBigMoneyAfter={shopAfterPurchase?.BigMoney ?? 0}",
            cancellationToken);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop purchase mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Offline-shop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from seller {SellerId} for {Price}",
            characterId, packet.SellItemIndex, listing.Quantity, sellerId.Value, listing.Price);

        return response;
    }

    private static bool IsExpiredDatedPage(UpdateProxyShopRequest packet, PlayerRuntimeState state)
    {
        return packet.SelfPage == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today();
    }

    private static OfflineShopItemRowDto? FindListing(IReadOnlyList<OfflineShopItemRowDto> items, short slotIndex,
        int expectedItemId)
    {
        foreach (var row in items)
            if (row.SlotIndex == slotIndex && row.ItemId == expectedItemId)
                return row;

        return null;
    }

    private static UpdateProxyShopResponse BuildReply(int result, int page, int index, ItemStack? stack, int money)
    {
        var value1 = stack is { } s
            ?
            [
                s.ItemId, 0, 0, s.Quantity, ItemValueCodec.Encode(s.Enchant, s.Combine, s.Refine, s.Socket), s.Serial,
                s.SocketGem1, s.SocketGem2, s.SocketGem3
            ]
            : new int[9];

        return new UpdateProxyShopResponse
        {
            Result = result,
            ProxyUser = ProxyShopWireMapper.Build(string.Empty, null, []),
            Page = page,
            Index = index,
            Value1 = value1,
            Money = money
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
