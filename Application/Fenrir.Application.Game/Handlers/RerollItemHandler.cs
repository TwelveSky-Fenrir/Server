using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Forge;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op26, CZ_EXCHANGE_ITEM_SEND -- rerolls a Rare/Elite equip item into a random same-tier/category
///     replacement via <see cref="RerollResolver" />. <c>Sort</c>/<c>Value1</c>/<c>Value2</c> are dead wire
///     fields the legacy handler itself never reads.
/// </summary>
public sealed class RerollItemHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<RerollItemHandler> logger)
    : IAsyncPacketHandler<RerollItemRequest>
{
    public async ValueTask HandleAsync(RerollItemRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(RerollItemRequest packet, IPacketSession session,
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

        var catalog = worldData.ItemsById.Values.Select(d => d.Item);
        var resolved = RerollResolver.Resolve(targetDefinition.Item, state.PreviousTribe, catalog,
            SystemRandomSource.Instance);

        if (resolved.Outcome == RerollResolver.RerollOutcome.Rejected)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (resolved.Outcome == RerollResolver.RerollOutcome.NoCandidate)
        {
            session.Send(new RerollItemResponse { Result = 1, Cost = resolved.Cost, Value = [0, 0, 0, 0, 0, 0] });
            return;
        }

        var newStack = target with { ItemId = resolved.ResultItemId };
        var projected = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newStack);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} reroll-item AdjustMoneyAndReplaceContainerAsync failed (treated as insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var packedValue = ItemValueCodec.Encode(target.Enchant, target.Combine, target.Refine, target.Socket);

        session.Send(new RerollItemResponse
        {
            Result = 0, Cost = resolved.Cost,
            Value = [resolved.ResultItemId, 0, 0, target.Quantity, packedValue, target.Serial]
        });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped reroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
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
