using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CaeriusNet.Exceptions;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class BuyCashItemService(
    ICashRepository cash,
    WorldDataCache worldData,
    CommerceCatalogCache catalog,
    ILogger<BuyCashItemService> logger) : IBuyCashItemService
{
    private const int ShopSpecificError = 60704;

    private const int InternalFailureResult = 1;

    private const int InsufficientCashErrorNumber = 50240;

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
            logger.LogDebug(
                "Buy cash item rejected: character {CharacterId} sent costInfoIndex {CostInfoIndex} outside catalog bounds",
                characterId, index);
            return new BuyCashItemResponse
            {
                Result = InternalFailureResult, CashSize = 0, Page = packet.Page, Index = packet.Index,
                Value = packet.Value
            };
        }

        var entry = costInfo[index];
        if (!entry.IsAssigned || !worldData.ItemsById.TryGetValue(entry.ItemId, out var itemDefinition))
        {
            logger.LogDebug(
                "Buy cash item rejected: character {CharacterId} costInfoIndex {CostInfoIndex} is unassigned or unresolvable",
                characterId, index);
            return new BuyCashItemResponse
            {
                Result = InternalFailureResult, CashSize = 0, Page = packet.Page, Index = packet.Index,
                Value = packet.Value
            };
        }

        if (!worldData.ItemsById.ContainsKey(packet.Value[0]))
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} supplied item {RequestedItemId} which is absent from the item catalog",
                characterId, packet.Value[0]);
            return null;
        }

        if (packet.Value[0] != entry.ItemId)
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} supplied item {RequestedItemId} for catalog entry {CostInfoIndex} whose item is {CatalogItemId}",
                characterId, packet.Value[0], index, entry.ItemId);
            return new BuyCashItemResponse
            {
                Result = InternalFailureResult, CashSize = 0, Page = packet.Page, Index = packet.Index,
                Value = packet.Value
            };
        }

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot) ||
            (page == ContainerMatrix.InventoryPage1 && state.InventoryDate < GameDate.Today()) ||
            packet.Value[1] is < 0 or > 7 || packet.Value[2] is < 0 or > 7)
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} sent invalid or expired-premium-page destination slot {Page}/{Index} -- session will be disconnected",
                characterId, page, slot);
            return null;
        }

        var destinationX = (byte)packet.Value[1];
        var destinationY = (byte)packet.Value[2];

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (isStackable && entry.Quantity is < 0 or > GroundItemPickupPolicy.MaxStackQuantity)
        {
            logger.LogInformation(
                "Buy cash item rejected: character {CharacterId} catalog quantity {CatalogQuantity} is outside the supported range",
                characterId, entry.Quantity);
            return new BuyCashItemResponse
            {
                Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
            };
        }

        var bulkCount = Math.Clamp(packet.Value[4], 1, 99);
        var unitsPerPurchase = isStackable && entry.Quantity > 0 ? entry.Quantity : 1;
        var totalQuantityLong = (long)unitsPerPurchase * bulkCount;

        if (totalQuantityLong > GroundItemPickupPolicy.MaxStackQuantity)
        {
            logger.LogInformation(
                "Buy cash item rejected: character {CharacterId} catalog quantity {CatalogQuantity} and bulk count {BulkCount} exceed one stack",
                characterId, entry.Quantity, bulkCount);
            return new BuyCashItemResponse
            {
                Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
            };
        }

        var totalQuantity = (int)totalQuantityLong;
        var chargeAmountLong = (long)entry.Cost * totalQuantity;
        if (chargeAmountLong is < 1 or > int.MaxValue)
        {
            logger.LogInformation(
                "Buy cash item rejected: character {CharacterId} catalog cost {CatalogCost} and total quantity {TotalQuantity} produce an invalid charge",
                characterId, entry.Cost, totalQuantity);
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
            if (!isStackable || existing.ItemId != entry.ItemId || existing.XPos != destinationX ||
                existing.YPos != destinationY)
            {
                logger.LogInformation(
                    "Buy cash item rejected: character {CharacterId} destination slot {Page}/{Index} is occupied by an incompatible stack",
                    characterId, page, slot);
                return new BuyCashItemResponse
                {
                    Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
                };
            }

            if (existing.Quantity is < 1 or > GroundItemPickupPolicy.MaxStackQuantity ||
                totalQuantity > GroundItemPickupPolicy.MaxStackQuantity - existing.Quantity)
            {
                logger.LogInformation(
                    "Buy cash item rejected: character {CharacterId} destination slot {Page}/{Index} cannot accept total quantity {TotalQuantity}",
                    characterId, page, slot, totalQuantity);
                return new BuyCashItemResponse
                {
                    Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
                };
            }

            newStack = existing with
            {
                Quantity = existing.Quantity + totalQuantity,
                Enchant = 0,
                Combine = 0,
                Refine = 0,
                Socket = 0,
                SocketGem1 = 0,
                SocketGem2 = 0,
                SocketGem3 = 0,
                ExpireDate = 0,
                Serial = 0,
                XPos = destinationX,
                YPos = destinationY
            };
        }
        else
        {
            var serial = ItemSerialGenerator.Generate(ItemSerialGenerator.EliteItemType, DateTimeOffset.UtcNow);
            newStack = new ItemStack(entry.ItemId, totalQuantity, 0, 0, 0, 0, 0, 0, 0, 0, serial,
                destinationX, destinationY);
        }

        var projectedContainer = state.Inventory.GetContainer((byte)page).SetItem((byte)slot, newStack);
        var projectedItems = ToTvps(projectedContainer);
        var operationId = Guid.NewGuid();
        var idempotencyKeyHash = SHA256.HashData(operationId.ToByteArray());
        var requestHash = ComputePurchaseEnvelopeHash(operationId, accountId, characterId, chargeAmount, 1,
            entry.ItemMallProductId, (byte)page, projectedItems, entry.ItemId, totalQuantity, newStack.Serial);

        int newBalance;
        try
        {
            newBalance = await cash.DebitAndGrantItemIdempotentAsync(operationId, idempotencyKeyHash, requestHash,
                accountId, chargeAmount, 1, entry.ItemMallProductId, characterId, (byte)page, projectedItems,
                cancellationToken, entry.ItemId, totalQuantity, newStack.Serial);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException
                                                {
                                                    Number: InsufficientCashErrorNumber
                                                })
        {
            logger.LogInformation(ex,
                "Account {AccountId} cash-shop purchase was rejected for insufficient cash",
                accountId);
            return new BuyCashItemResponse
                { Result = 2, CashSize = 0, Page = page, Index = slot, Value = packet.Value };
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Cash-shop purchase has an unknown durable outcome for account {AccountId}, character {CharacterId}; terminating the session before the purchase can be retried",
                accountId, characterId);
            state.Session.Abort(DisconnectReason.Faulted);
            return null;
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page, projectedContainer));
        var mirrorResult = await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken).ConfigureAwait(false);
        if (mirrorResult.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogCritical(
                "Cash-shop purchase committed for account {AccountId}, character {CharacterId}, but the zone actor did not apply its inventory mirror ({Outcome}); terminating the session so the durable inventory is rehydrated before another purchase",
                accountId, characterId, mirrorResult.Kind);
            state.Session.Abort(DisconnectReason.Faulted);
            return null;
        }

        state.LastCashItemPurchaseAtUtc = DateTime.UtcNow;

        var response = new BuyCashItemResponse
        {
            Result = 0, CashSize = newBalance, Page = page, Index = slot,
            Value = [newStack.ItemId, destinationX, destinationY, newStack.Quantity, 0, newStack.Serial]
        };

        logger.LogInformation(
            "Cash shop purchase completed: account {AccountId} character {CharacterId} bought item {ItemId} x{Quantity} for {ChargeAmount} cash (new balance {NewBalance})",
            accountId, characterId, entry.ItemId, totalQuantity, chargeAmount, newBalance);

        return response;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private static byte[] ComputePurchaseEnvelopeHash(Guid operationId, int accountId, int characterId, int amount,
        byte reason, int productId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, int auditItemId,
        int auditQuantity, int auditSerial)
    {
        var payload = new StringBuilder("cash-purchase/v1|");
        Append(payload, operationId);
        Append(payload, accountId);
        Append(payload, characterId);
        Append(payload, amount);
        Append(payload, reason);
        Append(payload, productId);
        Append(payload, container);
        Append(payload, auditItemId);
        Append(payload, auditQuantity);
        Append(payload, auditSerial);

        foreach (var item in items.OrderBy(item => item.Slot))
        {
            Append(payload, item.Slot);
            Append(payload, item.ItemId);
            Append(payload, item.Quantity);
            Append(payload, item.Enchant);
            Append(payload, item.Combine);
            Append(payload, item.Refine);
            Append(payload, item.Socket);
            Append(payload, item.SocketGem1);
            Append(payload, item.SocketGem2);
            Append(payload, item.SocketGem3);
            Append(payload, item.ExpireDate);
            Append(payload, item.Serial);
            Append(payload, item.XPos);
            Append(payload, item.YPos);
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static void Append(StringBuilder payload, object value)
    {
        payload.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        payload.Append('|');
    }
}
