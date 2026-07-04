using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_BLOOD_MARK_SEND (opcode 141, contracts/04_commerce.md, USE_BLOOD family, verified
///     <c>S04_MyWork02.cpp:15235-15372</c>). Legacy quirk preserved exactly (D8): the client's own
///     submitted <c>tValue[3]</c> ("buy quantity") is ONLY ever used as an anti-cheat plausibility bound
///     ([1, 999], stackable-only if &gt; 1) -- the quantity actually GRANTED always comes from the
///     catalog's own fixed <c>Quantity</c> for that slot, never from the client. D7 regime (b): BloodCoin
///     debit + the touched container commit atomically
///     (<see cref="CharacterRepository.SpendBloodCoinAndReplaceContainerAsync" />).
/// </summary>
public sealed class BuyBloodMarkItemHandler(
    CharacterRepository characters,
    WorldDataCache worldData,
    ILogger<BuyBloodMarkItemHandler> logger)
    : IAsyncPacketHandler<BuyBloodMarkItemRequest>
{
    private const int ShopSpecificError = 60704;

    public async ValueTask HandleAsync(BuyBloodMarkItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var bloodShop = BloodShopBuilder.Build(worldData.BloodExchangeCatalog, worldData.ItemsById);

        if (packet.BloodIndex < 0 || packet.BloodIndex >= bloodShop.BloodNum)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page = packet.Page;
        var slot = packet.Index;
        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, slot) ||
            packet.Value[1] is < 0 or > 7 || packet.Value[2] is < 0 or > 7)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var buyQuantity = packet.Value[3];
        if (buyQuantity < 1 || buyQuantity > GroundItemPickupPolicy.MaxStackQuantity)
        {
            session.Send(new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value });
            return;
        }

        if (!worldData.ItemsById.TryGetValue(packet.Value[0], out var itemDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);
        if (buyQuantity > 1 && !isStackable)
        {
            session.Send(new BuyBloodMarkItemResponse
                { Result = ShopSpecificError, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value });
            return;
        }

        var entry = bloodShop.Data[packet.BloodIndex];
        if (entry.ItemId != packet.Value[0])
        {
            // S04_MyWork02.cpp:15347 -- a stale client catalog view, a CLEAN reply (not a Quit()).
            session.Send(new BuyBloodMarkItemResponse
                { Result = 2, BloodCoin = 0, Page1 = page, Index1 = slot, Value = packet.Value });
            return;
        }

        var destination = state.Inventory.GetSlot((byte)page, (byte)slot);
        var mergesIntoExisting = destination is { } d && d.ItemId == entry.ItemId;

        int price;
        if (isStackable)
        {
            if (entry.Quantity < 1)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            price = entry.Price * entry.Quantity;

            if (mergesIntoExisting && destination!.Value.Quantity + entry.Quantity > GroundItemPickupPolicy.MaxStackQuantity)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else
        {
            price = entry.Price;
            if (destination is not null)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }
        }

        // Blood-mark items never carry sockets (verified wSetInvSock(...,0,0,0) unconditionally).
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
            // wAvatar.aBloodCoin < tPrice -- verified Quit()-worthy (S04_MyWork02.cpp:15321/15335), no clean-fail path.
            logger.LogWarning(ex,
                "Character {CharacterId} blood-mark purchase SpendBloodCoinAndReplaceContainerAsync failed (treated as insufficient BloodCoin)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new BuyBloodMarkItemResponse
        {
            Result = 0, BloodCoin = newBloodCoin, Page1 = page, Index1 = slot,
            Value = [entry.ItemId, packet.Value[1], packet.Value[2], finalQuantity, 0, 0]
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped blood-mark purchase mirror for character {CharacterId}",
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
