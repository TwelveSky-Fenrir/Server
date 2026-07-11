using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

/// <summary>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8118-8125 (200ms purchase-rate throttle, checked first and
///     ahead of every other gate -- hard disconnect, no soft reject exists for this opcode) ; :8126
///     (catalog-version gate -> Result=3) and :8145 (<c>mCashInfo-&gt;mIsSellCash == FALSE</c> shop-closed gate
///     -> Result=4), evaluated in that fixed order, both ahead of any other purchase logic (item lookup,
///     stack checks, debit, inventory write). None of these three gates has a production side effect beyond
///     answering (or disconnecting) the requester -- see
///     <see cref="Fenrir.Application.Game.Domain.Commerce.CommerceCatalogCache" /> for the live values the
///     version/shop-closed gates read.
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:134-152 (<c>CheckInv0</c>) -- the destination-slot gate
///     also disconnects when the destination page is the account's premium/rental inventory page
///     (<see cref="ContainerMatrix.InventoryPage1" />) and that page's <c>InventoryDate</c> expiry has
///     already passed, same "hard disconnect, no soft reject" posture as the out-of-bounds page/slot case
///     it is folded into below.
/// </summary>
public sealed class BuyCashItemService(
    ICashRepository cash,
    WorldDataCache worldData,
    CommerceCatalogCache catalog,
    IEventLogRepository eventLog,
    ILogger<BuyCashItemService> logger) : IBuyCashItemService
{
    // Shared "shop-specific" error code, reused across cash-shop-family rejects.
    private const int ShopSpecificError = 60704;

    // Server/ts25zone/S04_MyWork02.cpp:8118-8125 -- the handler's own hard-coded 200ms window.
    private static readonly TimeSpan PurchaseThrottleWindow = TimeSpan.FromMilliseconds(200);

    public async ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        // Purchase-rate throttle: only the *previous successful* purchase's timestamp is ever compared here,
        // and it is only advanced on this method's own success path below -- a burst of requests that all
        // fail an earlier gate (stale version, shop disabled, structural validation) never consumes or
        // resets this window (Server/ts25zone/S04_MyWork02.cpp:8118-8125,8243). Legacy hard-disconnects on
        // violation; there is no soft-reject response for this specific edge case.
        if (state.LastCashItemPurchaseAtUtc is { } lastPurchaseAt &&
            DateTime.UtcNow - lastPurchaseAt < PurchaseThrottleWindow)
        {
            logger.LogWarning(
                "Buy cash item rejected: character {CharacterId} violated the 200ms purchase-rate throttle -- session will be disconnected",
                characterId);
            return null;
        }

        // Catalog-version gate: a stale client is always told "your catalog is stale" (Result=3), never
        // "the shop is closed" (Result=4), even if the shop happens to also be closed right now -- fixed
        // evaluation order per the cited legacy source.
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

        // GL_604_BUY_CASH_ITEM parity (C20 audit-log contract) -- item/quantity/serial only, deliberately no
        // DeltaMoney (legacy discards the cost fields before any durable record -- see EventLogEmitters'
        // own remarks).
        await eventLog.LogCashShopPurchaseAsync(accountId, characterId, entry.ItemId, grantQuantity,
            newStack.Serial, cancellationToken);

        // Only advance the throttle timestamp once the debit/inventory write has actually succeeded --
        // matching legacy's own restamp point (Server/ts25zone/S04_MyWork02.cpp:8243), not on any earlier
        // rejection path above.
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
