using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_CASH_ITEM_SEND (opcode 42, contracts/04_commerce.md) -- purchase a cash-shop item. ANTI-CHEAT:
///     price and granted item/quantity are resolved ENTIRELY from <see cref="WorldDataCache.CashCatalog" />'s
///     <c>CostInfoIndex</c> lookup (<c>CashCatalogBuilder</c>, verified <c>MyDB::GetItemMall</c>) -- the
///     client's own submitted <c>Value[6]</c> is never trusted for price/item, only echoed back. D7 regime
///     (b): the account's cash debit + the character's item grant commit in ONE transaction
///     (<see cref="CashRepository.DebitAndGrantItemAsync" />).
/// </summary>
/// <remarks>
///     NOT modeled (documented, not guessed): the 200 ms runtime anti-spam gate (<c>mTickBuyCash</c>) and
///     the <c>Version</c> mismatch "invalidation branch", verified largely commented out in the legacy
///     source itself -- reproducing dead code would be a fabrication, not fidelity.
/// </remarks>
public sealed class BuyCashItemHandler(
    CashRepository cash,
    WorldDataCache worldData,
    ILogger<BuyCashItemHandler> logger)
    : IAsyncPacketHandler<BuyCashItemRequest>
{
    /// <summary><c>60704</c> -- the legacy's own "shop-specific" magic error code (verified reused for structural cash-shop rejects, e.g. CZ_BUY_BLOOD_MARK_SEND's identical reuse of ZC_BUY_CASH_ITEM_RECV).</summary>
    private const int ShopSpecificError = 60704;

    public async ValueTask HandleAsync(BuyCashItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        // Benign staleness (mid-handoff/disconnect) -- same defensive posture as every other InWorld handler.
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, accountId,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(BuyCashItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, int accountId,
        CancellationToken cancellationToken)
    {
        var index = packet.CostInfoIndex;
        if (index < 0 || index >= worldData.CashCatalog.CostInfoByIndex.Length)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var entry = worldData.CashCatalog.CostInfoByIndex[index];
        if (!entry.IsAssigned || !worldData.ItemsById.TryGetValue(entry.ItemId, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        var grantQuantity = isStackable ? Math.Max(1, entry.Quantity) : 1;
        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);

        ItemStack newStack;
        if (destination is { } existing)
        {
            if (!isStackable || existing.ItemId != entry.ItemId ||
                existing.Quantity + grantQuantity > GroundItemPickupPolicy.MaxStackQuantity)
            {
                session.Send(new BuyCashItemResponse
                {
                    Result = ShopSpecificError, CashSize = 0, Page = page, Index = slot, Value = packet.Value
                });
                return;
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
            newBalance = await cash.DebitAndGrantItemAsync(accountId, entry.Cost, reason: 1,
                entry.ItemMallProductId, characterId, (byte)page, ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            // Insufficient cash balance (usp_Cash_DebitAndGrantItem's own guard, shared with usp_Cash_Debit) --
            // logged so a transient/unrelated SQL fault is distinguishable from real insufficient funds.
            logger.LogWarning(ex,
                "Account {AccountId} cash-shop purchase DebitAndGrantItemAsync failed (treated as insufficient cash)",
                accountId);
            session.Send(new BuyCashItemResponse
                { Result = 2, CashSize = 0, Page = page, Index = slot, Value = packet.Value });
            return;
        }

        session.Send(new BuyCashItemResponse
        {
            Result = 0, CashSize = newBalance, Page = page, Index = slot,
            Value = [newStack.ItemId, 0, 0, newStack.Quantity, 0, 0]
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped cash-shop purchase mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
