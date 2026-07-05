using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_BUY_CASH_ITEM_SEND (opcode 42), extracted from <see cref="BuyCashItemHandler" />.</summary>
public interface IBuyCashItemService
{
    /// <summary>
    ///     Resolves and applies a cash-shop purchase. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);
}

public sealed class BuyCashItemService(
    ICashRepository cash,
    WorldDataCache worldData,
    ILogger<BuyCashItemService> logger) : IBuyCashItemService
{
    // Shared "shop-specific" error code, reused across cash-shop-family rejects.
    private const int ShopSpecificError = 60704;

    public async ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var index = packet.CostInfoIndex;
        if (index < 0 || index >= worldData.CashCatalog.CostInfoByIndex.Length)
            return null;

        var entry = worldData.CashCatalog.CostInfoByIndex[index];
        if (!entry.IsAssigned || !worldData.ItemsById.TryGetValue(entry.ItemId, out var itemDefinition))
            return null;

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot))
            return null;

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        var grantQuantity = isStackable ? Math.Max(1, entry.Quantity) : 1;
        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);

        ItemStack newStack;
        if (destination is { } existing)
        {
            if (!isStackable || existing.ItemId != entry.ItemId ||
                existing.Quantity + grantQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                return new BuyCashItemResponse
                {
                    Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
                };

            newStack = existing with { Quantity = existing.Quantity + grantQuantity };
        }
        else
        {
            newStack = new ItemStack(entry.ItemId, grantQuantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var projectedContainer = state.Inventory.GetContainer((byte)page).SetItem((byte)slot, newStack);

        int newBalance;
        try
        {
            newBalance = await cash.DebitAndGrantItemAsync(accountId, entry.Cost, 1,
                entry.ItemMallProductId, characterId, (byte)page, ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} cash-shop purchase DebitAndGrantItemAsync failed (treated as insufficient cash)",
                accountId);
            return new BuyCashItemResponse
                { Result = 2, CashSize = 0, Page = page, Index = slot, Value = packet.Value };
        }

        var response = new BuyCashItemResponse
        {
            Result = 0, CashSize = newBalance, Page = page, Index = slot,
            Value = [newStack.ItemId, 0, 0, newStack.Quantity, 0, 0]
        };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped cash-shop purchase mirror for character {CharacterId}",
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
