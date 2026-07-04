using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.StellarCores;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_STELLAR_STATE_SEND (op153). Sort 1-5 (Select/no-op/Equip/Remove/ReturnToInventory) match the legacy
///     switch exactly -- see <see cref="StellarCoreStateResolver" />'s remarks for why Select/Equip/Remove/
///     ReturnToInventorySuccess never actually fire against today's always-empty wardrobe. Same shape as
///     <see cref="CostumeStateHandler" />.
/// </summary>
public sealed class StellarCoreStateHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<StellarCoreStateHandler> logger)
    : IAsyncPacketHandler<StellarCoreStateRequest>
{
    public async ValueTask HandleAsync(StellarCoreStateRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(StellarCoreStateRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var context = new StellarCoreStateResolver.Context(state.StellarCoreIndex, state.StellarCoreWardrobe);
        var result = StellarCoreStateResolver.Resolve(packet.Sort, packet.Value, in context);

        switch (result.Kind)
        {
            case StellarCoreStateResolver.ResultKind.NoReply:
                return;

            case StellarCoreStateResolver.ResultKind.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case StellarCoreStateResolver.ResultKind.Select:
                session.Send(new StellarCoreStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1
                });
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex));
                return;

            case StellarCoreStateResolver.ResultKind.Equip:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new StellarCoreStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1
                });
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, result.NewCoreNumber, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Equip));
                return;
            }

            case StellarCoreStateResolver.ResultKind.Remove:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new StellarCoreStateResponse
                {
                    Result = 0, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1
                });
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, 0, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Remove));
                return;
            }

            case StellarCoreStateResolver.ResultKind.ReturnToInventoryMismatch:
                session.Send(new StellarCoreStateResponse
                {
                    Result = 1, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                    ItemIndex = -1
                });
                return;

            case StellarCoreStateResolver.ResultKind.ReturnToInventorySuccess:
                await GrantCoreToInventoryAsync(packet, session, zone, state, characterId, result,
                    cancellationToken);
                return;
        }
    }

    private async ValueTask GrantCoreToInventoryAsync(StellarCoreStateRequest packet, IPacketSession session,
        Zone zone, PlayerRuntimeState state, int characterId, StellarCoreStateResolver.Result result,
        CancellationToken cancellationToken)
    {
        if (!worldData.ItemsById.TryGetValue(result.GrantedItemId, out _))
        {
            session.Send(new StellarCoreStateResponse
            {
                Result = 2, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                ItemIndex = -1
            });
            return;
        }

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
        {
            session.Send(new StellarCoreStateResponse
            {
                Result = 2, Sort = packet.Sort, Value = packet.Value, Page = -1, PosX = -1, PosY = -1,
                ItemIndex = -1
            });
            return;
        }

        var newStack = new ItemStack(result.GrantedItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer =
            state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        await characters.ReplaceContainerAsync(characterId, destination.Container, ToTvps(projectedContainer),
            cancellationToken);

        session.Send(new StellarCoreStateResponse
        {
            Result = 0, Sort = packet.Sort, Value = packet.Value, Page = destination.Container, PosX = 0, PosY = 0,
            ItemIndex = result.GrantedItemId
        });

        zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId, result.NewCoreIndex,
            WardrobeSlotCleared: result.ClearedSlot));

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped stellar-core-return-to-inventory mirror for character {CharacterId}",
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
