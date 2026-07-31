using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class BuyBloodMarkItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    CommerceCatalogCache catalog,
    ILogger<BuyBloodMarkItemService> logger) : IBuyBloodMarkItemService
{
    private const int ShopSpecificError = 60704;

    public async ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var bloodShop = BloodShopBuilder.Build(catalog.BloodExchangeCatalog, worldData.ItemsById);

        if (packet.BloodIndex < 0 || packet.BloodIndex >= bloodShop.BloodNum)
        {
            logger.LogWarning(
                "Buy blood mark item rejected: character {CharacterId} sent out-of-range bloodIndex {BloodIndex} -- session will be disconnected",
                characterId, packet.BloodIndex);
            return null;
        }

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot) ||
            (page == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today()) ||
            packet.Value[1] is < 0 or > 7 || packet.Value[2] is < 0 or > 7)
        {
            logger.LogWarning(
                "Buy blood mark item rejected: character {CharacterId} sent invalid or expired-premium-page destination slot {Page}/{Index} -- session will be disconnected",
                characterId, page, slot);
            return null;
        }

        var buyQuantity = packet.Value[3];
        if (buyQuantity is < 1 or > GroundItemPickupPolicy.MaxStackQuantity)
        {
            logger.LogInformation(
                "Buy blood mark item rejected: character {CharacterId} sent out-of-range quantity {BuyQuantity}",
                characterId, buyQuantity);
            return new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };
        }

        if (!worldData.ItemsById.TryGetValue(packet.Value[0], out var itemDefinition))
        {
            logger.LogWarning(
                "Buy blood mark item rejected: character {CharacterId} item {ItemId} is unresolvable -- session will be disconnected",
                characterId, packet.Value[0]);
            return null;
        }

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (buyQuantity > 1 && !isStackable)
        {
            logger.LogInformation(
                "Buy blood mark item rejected: character {CharacterId} requested quantity {BuyQuantity} for non-stackable item {ItemId}",
                characterId, buyQuantity, packet.Value[0]);
            return new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };
        }

        var entry = bloodShop.Data[packet.BloodIndex];
        if (entry.ItemId != packet.Value[0])
        {
            logger.LogInformation(
                "Buy blood mark item rejected: character {CharacterId} bloodIndex {BloodIndex} item mismatch (catalog {CatalogItemId}, requested {RequestedItemId})",
                characterId, packet.BloodIndex, entry.ItemId, packet.Value[0]);
            return new BuyBloodMarkItemResponse
                { Result = 2, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };
        }

        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);
        var mergesIntoExisting = destination is { } d && d.ItemId == entry.ItemId;

        int price;
        if (isStackable)
        {
            if (entry.Quantity < 1)
            {
                logger.LogWarning(
                    "Buy blood mark item rejected: character {CharacterId} bloodIndex {BloodIndex} has a non-positive catalog quantity -- session will be disconnected",
                    characterId, packet.BloodIndex);
                return null;
            }

            price = entry.Price * entry.Quantity;

            if (mergesIntoExisting &&
                destination!.Value.Quantity + entry.Quantity > GroundItemPickupPolicy.MaxStackQuantity)
            {
                logger.LogWarning(
                    "Buy blood mark item rejected: character {CharacterId} destination slot {Page}/{Index} would overflow the max stack quantity -- session will be disconnected",
                    characterId, page, slot);
                return null;
            }
        }
        else
        {
            price = entry.Price;
            if (destination is not null)
            {
                logger.LogWarning(
                    "Buy blood mark item rejected: character {CharacterId} destination slot {Page}/{Index} is occupied (non-stackable item) -- session will be disconnected",
                    characterId, page, slot);
                return null;
            }
        }

        var finalQuantity = mergesIntoExisting ? destination!.Value.Quantity + entry.Quantity : entry.Quantity;
        var newStack = new ItemStack(entry.ItemId, finalQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer = state.Inventory.GetContainer((byte)page).SetItem((byte)slot, newStack);

        int newBloodCoin;
        try
        {
            newBloodCoin = await characters.SpendBloodCoinAndReplaceContainerAsync(characterId, -price, (byte)page,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} blood-mark purchase SpendBloodCoinAndReplaceContainerAsync failed (treated as insufficient BloodCoin)",
                characterId);
            return null;
        }

        var response = new BuyBloodMarkItemResponse
        {
            Result = 0, BloodCoin = newBloodCoin, Page1 = page, Index1 = slot,
            Value = [entry.ItemId, packet.Value[1], packet.Value[2], finalQuantity, 0, 0]
        };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped blood-mark purchase mirror for character {CharacterId}",
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
