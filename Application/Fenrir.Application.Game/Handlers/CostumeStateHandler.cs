using System.Collections.Immutable;
using Fenrir.Application.Game.Costumes;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_COSTUME_STATE_SEND (op90). Sort 1-5 (Select/no-op/Equip/Remove/ReturnToInventory) match the legacy
///     switch exactly -- see <see cref="CostumeStateResolver" />'s remarks for why Select/Equip/Remove/
///     ReturnToInventorySuccess never actually fire against today's always-empty wardrobe.
/// </summary>
public sealed class CostumeStateHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<CostumeStateHandler> logger)
    : IAsyncPacketHandler<CostumeStateRequest>
{
    public async ValueTask HandleAsync(CostumeStateRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(CostumeStateRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var context = new CostumeStateResolver.Context(state.CostumeIndex, state.CostumeWardrobe);
        var result = CostumeStateResolver.Resolve(packet.Sort, packet.Value, in context);

        switch (result.Kind)
        {
            case CostumeStateResolver.ResultKind.NoReply:
                return;

            case CostumeStateResolver.ResultKind.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case CostumeStateResolver.ResultKind.Select:
                session.Send(new CostumeStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1, CostumeDate = 0
                });
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex));
                return;

            case CostumeStateResolver.ResultKind.Equip:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new CostumeStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1, CostumeDate = 0
                });
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
                    result.NewCostumeNumber, Life: maxLife, Mana: maxMana,
                    Broadcast: CostumeBroadcastKind.Equip));
                return;
            }

            case CostumeStateResolver.ResultKind.Remove:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new CostumeStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1, CostumeDate = 0
                });
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
                    0, Life: maxLife, Mana: maxMana, Broadcast: CostumeBroadcastKind.Remove));
                return;
            }

            case CostumeStateResolver.ResultKind.ReturnToInventoryMismatch:
                session.Send(new CostumeStateResponse
                {
                    Result = 1, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1, CostumeDate = 0
                });
                return;

            case CostumeStateResolver.ResultKind.ReturnToInventorySuccess:
                await GrantCostumeToInventoryAsync(packet, session, zone, state, characterId, result,
                    cancellationToken);
                return;
        }
    }

    private async ValueTask GrantCostumeToInventoryAsync(CostumeStateRequest packet, IPacketSession session,
        Zone zone, PlayerRuntimeState state, int characterId, CostumeStateResolver.Result result,
        CancellationToken cancellationToken)
    {
        if (!worldData.ItemsById.TryGetValue(result.GrantedItemId, out _))
        {
            session.Send(new CostumeStateResponse
            {
                Result = 2, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                ItemIndex = -1, CostumeDate = 0
            });
            return;
        }

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
        {
            session.Send(new CostumeStateResponse
            {
                Result = 2, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                ItemIndex = -1, CostumeDate = 0
            });
            return;
        }

        var newStack = new ItemStack(result.GrantedItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer =
            state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        await characters.ReplaceContainerAsync(characterId, destination.Container, ToTvps(projectedContainer),
            cancellationToken);

        session.Send(new CostumeStateResponse
        {
            Result = 0, Sort = packet.Sort, Value = packet.Value, Page = destination.Container, PosX = 0, PosY = 0,
            ItemIndex = result.GrantedItemId, CostumeDate = 0
        });

        zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
            WardrobeSlotCleared: result.ClearedSlot));

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped costume-return-to-inventory mirror for character {CharacterId}",
                zone.MapId, characterId);
    }

    private static (byte Container, byte Slot)? FindFreeSlot(InventoryState inventory)
    {
        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage0, slot) is null)
                return (ContainerMatrix.InventoryPage0, slot);

        for (byte slot = 0; slot <= 63; slot++)
            if (inventory.GetSlot(ContainerMatrix.InventoryPage1, slot) is null)
                return (ContainerMatrix.InventoryPage1, slot);

        return null;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
