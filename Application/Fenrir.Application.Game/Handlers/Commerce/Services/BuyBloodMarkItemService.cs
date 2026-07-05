using System.Collections.Immutable;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_BUY_BLOOD_MARK_SEND (opcode 141), extracted from <see cref="BuyBloodMarkItemHandler" />.</summary>
public interface IBuyBloodMarkItemService
{
    /// <summary>
    ///     Resolves and applies a blood-mark purchase. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}

public sealed class BuyBloodMarkItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<BuyBloodMarkItemService> logger) : IBuyBloodMarkItemService
{
    private const int ShopSpecificError = 60704;

    public async ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var bloodShop = BloodShopBuilder.Build(worldData.BloodExchangeCatalog, worldData.ItemsById);

        if (packet.BloodIndex < 0 || packet.BloodIndex >= bloodShop.BloodNum)
            return null;

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot) ||
            packet.Value[1] is < 0 or > 7 || packet.Value[2] is < 0 or > 7)
            return null;

        var buyQuantity = packet.Value[3];
        if (buyQuantity is < 1 or > GroundItemPickupPolicy.MaxStackQuantity)
            return new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };

        if (!worldData.ItemsById.TryGetValue(packet.Value[0], out var itemDefinition))
            return null;

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (buyQuantity > 1 && !isStackable)
            return new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };

        var entry = bloodShop.Data[packet.BloodIndex];
        if (entry.ItemId != packet.Value[0])
            return new BuyBloodMarkItemResponse
                { Result = 2, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value };

        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);
        var mergesIntoExisting = destination is { } d && d.ItemId == entry.ItemId;

        int price;
        if (isStackable)
        {
            if (entry.Quantity < 1)
                return null;

            price = entry.Price * entry.Quantity;

            if (mergesIntoExisting &&
                destination!.Value.Quantity + entry.Quantity > GroundItemPickupPolicy.MaxStackQuantity)
                return null;
        }
        else
        {
            price = entry.Price;
            if (destination is not null)
                return null;
        }

        // Blood-mark items never carry sockets.
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
