using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

/// <summary>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8126 (catalog-version gate -> Result=3) and :8145
///     (<c>mCashInfo-&gt;mIsSellCash == FALSE</c> shop-closed gate -> Result=4), evaluated in that fixed order,
///     both ahead of any other purchase logic (item lookup, stack checks, debit, inventory write). Neither
///     gate has a production side effect beyond answering the requester -- see
///     <see cref="Fenrir.Application.Game.Domain.Commerce.CommerceCatalogCache" /> for the live values these
///     gates read.
/// </summary>
public sealed class BuyCashItemService(
    ICashRepository cash,
    WorldDataCache worldData,
    CommerceCatalogCache catalog,
    ILogger<BuyCashItemService> logger) : IBuyCashItemService
{
    // Shared "shop-specific" error code, reused across cash-shop-family rejects.
    private const int ShopSpecificError = 60704;

    public async ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        // Catalog-version gate: a stale client is always told "your catalog is stale" (Result=3), never
        // "the shop is closed" (Result=4), even if the shop happens to also be closed right now -- fixed
        // evaluation order per the cited legacy source.
        if (packet.Version != catalog.CashCatalogVersion)
            return new BuyCashItemResponse
            {
                Result = 3, CashSize = 0, Page = packet.Page, Index = packet.Index, Value = packet.Value
            };

        if (!catalog.CashShopSellEnabled)
            return new BuyCashItemResponse
            {
                Result = 4, CashSize = 0, Page = packet.Page, Index = packet.Index, Value = packet.Value
            };

        var costInfo = catalog.CashCatalog.CostInfoByIndex;
        var index = packet.CostInfoIndex;
        if (index < 0 || index >= costInfo.Length)
            return null;

        var entry = costInfo[index];
        if (!entry.IsAssigned || !worldData.ItemsById.TryGetValue(entry.ItemId, out var itemDefinition))
            return null;

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot))
            return null;

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        // Legacy "custom bulk item mall" feature: the client-submitted count at Value[4] is clamped to
        // 1-99 and multiplies the single catalog-defined quantity before it is granted
        // (Server/ts25zone/S04_MyWork02.cpp:8208-8223, "Extra validates and charges only one purchase. We
        // multiply the received quantity here for custom bulk item mall."). Legacy's own charge call
        // charges entry.Cost for exactly one unit regardless of this multiplier -- a real-money-adjacent
        // duplication/value-creation defect (grant scales with the client-controlled multiplier, charge
        // does not), not a deliberate design. Fenrir deliberately DIVERGES from that literal legacy
        // behavior here and scales the charge by the same bulkCount as the grant, per this project's
        // standing "harden in Fenrir, never reproduce" policy for known legacy currency/duplication
        // exploits (see the ts25-security-findings-catalog skill and ServerDocs/04_SECURITE_ET_DETTE_TECHNIQUE.md).
        // Deliberately scoped to stackable items only: the legacy multiplication step runs unconditionally
        // regardless of item category, but what bound (if any) `InvEqual0` enforces for non-duplicable
        // categories is not observed in the cited range, so extending the multiplier to non-stackable
        // items is left unimplemented pending that open question rather than guessed at (see the
        // "Non-duplicable item categories" edge case in this finding's behavior contract).
        var bulkCount = Math.Clamp(packet.Value[4], 1, 99);
        var grantQuantity = isStackable ? Math.Max(1, entry.Quantity) * bulkCount : 1;
        var chargeAmountLong = isStackable ? (long)entry.Cost * bulkCount : entry.Cost;

        // Legacy's own pre-purchase duplication-limit check runs against the raw, un-multiplied client
        // quantity (Server/ts25zone/S04_MyWork02.cpp:8162-8180) and the cited source never re-validates
        // the multiplied result before the final inventory write -- flagged in this finding's behavior
        // contract as a possible legacy overflow/item-loss defect, not a confirmed intentional behavior.
        // Fenrir does not reproduce that gap: capping the multiplied grant at MaxStackQuantity here keeps
        // the same invariant every other bulk-quantity purchase flow in this codebase already enforces
        // (e.g. BuyBloodMarkItemService), rather than risking an ItemStack.Quantity above what the rest of
        // the domain layer assumes. The same overflow-safety reasoning applies to chargeAmountLong: computed
        // in long, then range-checked here rather than left to overflow int silently at the debit call.
        if (grantQuantity > GroundItemPickupPolicy.MaxStackQuantity || chargeAmountLong > int.MaxValue)
            return new BuyCashItemResponse
            {
                Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
            };

        var chargeAmount = (int)chargeAmountLong;

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
            newBalance = await cash.DebitAndGrantItemAsync(accountId, chargeAmount, 1,
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
