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

public sealed class BuyCashItemService(
    ICashRepository cash,
    WorldDataCache worldData,
    CommerceCatalogCache catalog,
    ILogger<BuyCashItemService> logger) : IBuyCashItemService
{
    private const int ShopSpecificError = 60704;

    private static readonly TimeSpan PurchaseThrottleWindow = TimeSpan.FromMilliseconds(200);

    public async ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (state.LastCashItemPurchaseAtUtc is { } lastPurchaseAt &&
            DateTime.UtcNow - lastPurchaseAt < PurchaseThrottleWindow)
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} violated the 200ms purchase-rate throttle -- session will be disconnected",
                characterId);
            return null;
        }

        if (packet.Version != catalog.CashCatalogVersion)
        {
            logger.LogDebug(
                "Buy cash item rejected: character {CharacterId} stale catalog version (client {ClientVersion}, live {LiveVersion})",
                characterId, packet.Version, catalog.CashCatalogVersion);
            return new BuyCashItemResponse
            {
                Result = 3, CashSize = 0, Page = packet.Page, Index = packet.Index, Value = packet.Value
            };
        }

        if (!catalog.CashShopSellEnabled)
        {
            logger.LogInformation("Buy cash item rejected: character {CharacterId} cash shop is currently closed",
                characterId);
            return new BuyCashItemResponse
            {
                Result = 4, CashSize = 0, Page = packet.Page, Index = packet.Index, Value = packet.Value
            };
        }

        var costInfo = catalog.CashCatalog.CostInfoByIndex;
        var index = packet.CostInfoIndex;
        if (index < 0 || index >= costInfo.Length)
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} sent out-of-range costInfoIndex {CostInfoIndex} -- session will be disconnected",
                characterId, index);
            return null;
        }

        var entry = costInfo[index];
        if (!entry.IsAssigned || !worldData.ItemsById.TryGetValue(entry.ItemId, out var itemDefinition))
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} costInfoIndex {CostInfoIndex} is unassigned or unresolvable -- session will be disconnected",
                characterId, index);
            return null;
        }

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot) ||
            (page == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today()))
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} sent invalid or expired-premium-page destination slot {Page}/{Index} -- session will be disconnected",
                characterId, page, slot);
            return null;
        }

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        var bulkCount = Math.Clamp(packet.Value[4], 1, 99);
        var grantQuantity = isStackable ? Math.Max(1, entry.Quantity) * bulkCount : 1;
        var chargeAmountLong = isStackable ? (long)entry.Cost * bulkCount : entry.Cost;

        if (grantQuantity > GroundItemPickupPolicy.MaxStackQuantity || chargeAmountLong > int.MaxValue)
        {
            logger.LogInformation(
                "Buy cash item rejected: character {CharacterId} bulk count {BulkCount} would overflow grant quantity or charge amount",
                characterId, bulkCount);
            return new BuyCashItemResponse
            {
                Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
            };
        }

        var chargeAmount = (int)chargeAmountLong;

        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);

        ItemStack newStack;
        if (destination is { } existing)
        {
            if (!isStackable || existing.ItemId != entry.ItemId ||
                existing.Quantity + grantQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            {
                logger.LogInformation(
                    "Buy cash item rejected: character {CharacterId} destination slot {Page}/{Index} cannot accept item {ItemId} x{Quantity}",
                    characterId, page, slot, entry.ItemId, grantQuantity);
                return new BuyCashItemResponse
                {
                    Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
                };
            }

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
            newBalance = await cash.DebitAndGrantItemAsync(accountId, chargeAmount, 1,
                entry.ItemMallProductId, characterId, (byte)page, ToTvps(projectedContainer), cancellationToken,
                entry.ItemId, grantQuantity, newStack.Serial);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Account {AccountId} cash-shop purchase DebitAndGrantItemAsync failed (treated as insufficient cash)",
                accountId);
            return new BuyCashItemResponse
                { Result = 2, CashSize = 0, Page = page, Index = slot, Value = packet.Value };
        }

        state.LastCashItemPurchaseAtUtc = DateTime.UtcNow;

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

        logger.LogInformation(
            "Cash shop purchase completed: account {AccountId} character {CharacterId} bought item {ItemId} x{Quantity} for {ChargeAmount} cash (new balance {NewBalance})",
            accountId, characterId, entry.ItemId, grantQuantity, chargeAmount, newBalance);

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
