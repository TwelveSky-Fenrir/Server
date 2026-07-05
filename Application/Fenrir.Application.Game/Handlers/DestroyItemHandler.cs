using System.Collections.Immutable;
using Fenrir.Application.Game.Forge;
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
///     op89, CZ_DESTROY_ITEM_SEND -- dissolves an enchanted Rare equip item into money plus a compensation
///     stone. Only the LNW33 (EU33) branch is reproduced (Elite items are always rejected in this build, see
///     <see cref="DestroyResolver" />'s remarks).
/// </summary>
public sealed class DestroyItemHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DestroyItemHandler> logger)
    : IAsyncPacketHandler<DestroyItemRequest>
{
    public async ValueTask HandleAsync(DestroyItemRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(DestroyItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (targetStack is not { } target || !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = DestroyResolver.Resolve(targetDefinition.Item, target);
        if (resolved.Outcome == DestroyResolver.DestroyOutcome.Rejected ||
            !worldData.ItemsById.TryGetValue(resolved.StoneItemId, out var stoneDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var quantity = stoneDefinition.Item.Sort == 99 ? 1 : 0;
        var newStack = new ItemStack(resolved.StoneItemId, quantity, 0, 0, 0, 0, 0, 0, 0, target.ExpireDate,
            target.Serial);

        var projected = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newStack);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, resolved.Money, 0, (byte)page1,
                ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} destroy-item AdjustMoneyAndReplaceContainerAsync failed (treated as money-cap overflow)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new DestroyItemResponse
        {
            Result = 0, Money = resolved.Money, Value = [resolved.StoneItemId, 0, 0, quantity, 0, target.Serial]
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped destroy-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
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
