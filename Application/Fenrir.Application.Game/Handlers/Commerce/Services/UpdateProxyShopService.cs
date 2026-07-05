using System.Collections.Immutable;
using Fenrir.Application.Game.Handlers.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

public readonly record struct UpdateProxyShopValidation(bool Abort, short SlotIndex, ItemDefinition? ItemDefinition);

/// <summary>Business logic for CZ_SET_DEPUTY_PSHOP_SEND (opcode 109), extracted from <see cref="UpdateProxyShopHandler" />.</summary>
public interface IUpdateProxyShopService
{
    /// <summary>Shared pre-lock validation for both <c>BuySort</c> branches.</summary>
    UpdateProxyShopValidation Validate(UpdateProxyShopRequest packet);

    /// <summary>
    ///     <c>BuySort</c> 1 -- RETRIEVE an unsold item from the caller's own closed shop back to inventory.
    ///     Returns <c>null</c> when the caller should abort the session as faulted.
    /// </summary>
    ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>BuySort</c> 2 -- PURCHASE from another character's open shop. Only the buyer/retriever is ever a
    ///     live participant -- the seller's shop lives purely in SQL, so no dual-lock is needed here (unlike
    ///     <c>BuyShopItemService</c>'s live-PShop twin). Returns <c>null</c> when the caller should abort the
    ///     session as faulted.
    /// </summary>
    ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);
}

/// <remarks>
///     The legacy's larger result-code taxonomy is collapsed here to 0 (success) / 1 (mismatch or unknown
///     shop) / 2 (insufficient funds or a cap).
/// </remarks>
public sealed class UpdateProxyShopService(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<UpdateProxyShopService> logger) : IUpdateProxyShopService
{
    public UpdateProxyShopValidation Validate(UpdateProxyShopRequest packet)
    {
        if (packet.BuySort is not (1 or 2))
            return new UpdateProxyShopValidation(true, 0, null);

        var slotIndex = (short)(packet.SellPage * 5 + packet.SellIndex);
        if (packet.SellPage is < 0 or >= 5 || packet.SellIndex is < 0 or >= 5 ||
            packet.SelfPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.SelfPage, packet.SelfIndex) ||
            packet.SelfX is < 0 or > 7 || packet.SelfY is < 0 or > 7)
            return new UpdateProxyShopValidation(true, 0, null);

        if (!worldData.ItemsById.TryGetValue(packet.SellItemIndex, out var itemDefinition))
            return new UpdateProxyShopValidation(true, 0, null);

        return new UpdateProxyShopValidation(false, slotIndex, itemDefinition);
    }

    public async ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
            return null;

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
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, 0);
        }

        var response = BuildReply(0, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, 0);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop retrieve mirror for character {CharacterId}",
                zone.MapId, characterId);

        return response;
    }

    public async ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, null, packet.Socket, 0);

        // Buying from one's own open shop would bypass RETRIEVE's "must be closed" gate and refund the
        // price into the shop's own earnings -- rejected as the safe, conservative choice.
        if (sellerId.Value == characterId)
            return null;

        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
            return null;

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
            return BuildReply(2, packet.SelfPage, packet.SelfIndex, null, packet.Socket, 0);
        }

        var response = BuildReply(0, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, packet.Price);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop purchase mirror for character {CharacterId}",
                zone.MapId, characterId);

        return response;
    }

    private static UpdateProxyShopResponse BuildReply(int result, int page, int index, ItemStack? stack,
        int[] requestSocket, int money)
    {
        var value1 = stack is { } s
            ?
            [
                s.ItemId, 0, 0, s.Quantity, ItemValueCodec.Encode(s.Enchant, s.Combine, s.Refine, s.Socket), s.Serial,
                requestSocket[0], requestSocket[1], requestSocket[2]
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
