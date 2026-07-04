using System.Collections.Immutable;
using Fenrir.Application.Game.Consumables;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op23, CZ_USE_INVENTORY_ITEM_SEND -- only the Bottle family (iSort==26, S04_MyWork03.cpp:2448) is
///     modeled, via <see cref="BottleResolver.ResolveAcquire" />: it's the one family with no item-catalog or
///     other-subsystem (mounts, skills, costumes, cash-shop timers...) dependency Fenrir doesn't already have,
///     and unblocks <see cref="DrinkBottleHandler" />'s end-to-end path. Every other iSort/iIndex family in the
///     ~6300-line legacy switch replies with a clean Result=1 failure rather than reproducing its real
///     per-family behavior -- out of scope for this pass (see the recon report's own Batch A conclusion).
/// </summary>
/// <remarks>
///     Not modeled: the per-tick anti-flood throttle (mTickForUseInventoryItem) and the page-1
///     storage-extension gate (aInventoryDate, no <see cref="PlayerRuntimeState" /> field exists yet) -- both
///     orthogonal to which item family is used, and neither has an acquisition/observation path through any
///     opcode implemented so far.
/// </remarks>
public sealed class UseInventoryItemHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<UseInventoryItemHandler> logger)
    : IAsyncPacketHandler<UseInventoryItemRequest>
{
    private const byte BottleSort = 26;

    public async ValueTask HandleAsync(UseInventoryItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page;
        var index = packet.Index;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zone, state, characterId, (byte)page, (byte)index,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(UseInventoryItemRequest packet, IPacketSession session, Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition) ||
            itemDefinition.Item.Sort != BottleSort)
        {
            session.Send(Fail(page, index));
            return;
        }

        var resolved = BottleResolver.ResolveAcquire(state.BottleSlots, item.ItemId);
        if (resolved.Outcome == BottleResolver.AcquireOutcome.Rejected)
        {
            session.Send(Fail(page, index));
            return;
        }

        var projected = state.Inventory.GetContainer(page).Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        session.Send(new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = resolved.SlotIndex, Value2 = 0
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item (bottle) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, resolved.SlotIndex,
                resolved.RefilledCount, state.Life, item.ItemId)))
            logger.LogError(
                "Zone {MapId} bottle inbox full: dropped bottle-acquire mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static UseInventoryItemResponse Fail(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
