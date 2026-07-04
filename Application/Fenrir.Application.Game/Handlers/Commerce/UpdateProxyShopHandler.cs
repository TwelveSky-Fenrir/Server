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
///     CZ_SET_DEPUTY_PSHOP_SEND (opcode 109, contracts/04_commerce.md, verified <c>S07_MyGame09.cpp:557-884</c>).
///     <c>BuySort</c> 1 = RETRIEVE an unsold item from the CALLER'S OWN closed shop back to their own
///     inventory; <c>BuySort</c> 2 = PURCHASE from ANOTHER character's OPEN shop (<c>AvatarName</c>
///     resolves the seller). Only the buyer/retriever is ever a live participant -- the seller's shop
///     money/items live purely in SQL (game.OfflineShops), so no dual-lock is needed here (unlike
///     <c>BuyShopItemHandler</c>'s live-PShop twin).
/// </summary>
/// <remarks>
///     SCOPE SIMPLIFICATION: the legacy's result-code taxonomy for this action is large
///     (100/101/102/1/2/4/5/1000...) -- collapsed here to 0 (success) / 1 (mismatch or unknown shop) / 2
///     (insufficient funds or a cap); every failure still cleanly replies except genuinely
///     malformed/out-of-range wire fields.
/// </remarks>
public sealed class UpdateProxyShopHandler(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<UpdateProxyShopHandler> logger)
    : IAsyncPacketHandler<UpdateProxyShopRequest>
{
    public async ValueTask HandleAsync(UpdateProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.BuySort is not (1 or 2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slotIndex = (short)(packet.SellPage * 5 + packet.SellIndex);
        if (packet.SellPage is < 0 or >= 5 || packet.SellIndex is < 0 or >= 5 ||
            packet.SelfPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.SelfPage, packet.SelfIndex) ||
            packet.SelfX is < 0 or > 7 || packet.SelfY is < 0 or > 7)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(packet.SellItemIndex, out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            if (packet.BuySort == 1)
                await RetrieveAsync(packet, session, zoneSession, zone, state, characterId, slotIndex,
                    itemDefinition, cancellationToken);
            else
                await PurchaseAsync(packet, session, zoneSession, zone, state, characterId, slotIndex,
                    itemDefinition, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask RetrieveAsync(UpdateProxyShopRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, short slotIndex,
        ItemDefinition itemDefinition, CancellationToken cancellationToken)
    {
        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var finalQuantity = destination is { } d ? d.Quantity + packet.Quantity : packet.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packet.Value);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            packet.Socket[0], packet.Socket[1], packet.Socket[2], 0, packet.Serial);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.RetrieveItemAndReplaceContainerAsync(characterId, slotIndex, packet.SellItemIndex,
                packet.Quantity, packet.Value, (byte)packet.SelfPage, ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop retrieve RetrieveItemAndReplaceContainerAsync failed",
                characterId);
            Reply(session, 1, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, money: 0);
            return;
        }

        Reply(session, 0, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, money: 0);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop retrieve mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private async ValueTask PurchaseAsync(UpdateProxyShopRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, short slotIndex,
        ItemDefinition itemDefinition, CancellationToken cancellationToken)
    {
        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
        {
            Reply(session, 1, packet.SelfPage, packet.SelfIndex, null, packet.Socket, money: 0);
            return;
        }

        // Buying from ONE'S OWN open shop would return the item to live inventory while ShopState stays
        // OPEN, bypassing RETRIEVE's "must be closed" gate and refunding the price into the shop's own
        // earnings. No legacy evidence either way; rejected as the safe, conservative choice.
        if (sellerId.Value == characterId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var finalQuantity = destination is { } d ? d.Quantity + packet.Quantity : packet.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packet.Value);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            packet.Socket[0], packet.Socket[1], packet.Socket[2], 0, packet.Serial);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.ExecutePurchaseAsync(sellerId.Value, slotIndex, packet.SellItemIndex, packet.Quantity,
                packet.Value, packet.Price, characterId, (byte)packet.SelfPage, ToTvps(projectedContainer),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} offline-shop purchase ExecutePurchaseAsync failed",
                characterId);
            Reply(session, 2, packet.SelfPage, packet.SelfIndex, null, packet.Socket, money: 0);
            return;
        }

        Reply(session, 0, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, packet.Price);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop purchase mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static void Reply(IPacketSession session, int result, int page, int index, ItemStack? stack,
        int[] requestSocket, int money)
    {
        int[] value1 = stack is { } s
            ? [s.ItemId, 0, 0, s.Quantity, ItemValueCodec.Encode(s.Enchant, s.Combine, s.Refine, s.Socket), s.Serial,
                requestSocket[0], requestSocket[1], requestSocket[2]]
            : new int[9];

        session.Send(new UpdateProxyShopResponse
        {
            Result = result,
            ProxyUser = ProxyShopWireMapper.Build(string.Empty, null, []),
            Page = page,
            Index = index,
            Value1 = value1,
            Money = money
        });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
