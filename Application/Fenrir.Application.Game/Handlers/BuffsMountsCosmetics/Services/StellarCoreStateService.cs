using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.StellarCores;
using Fenrir.Application.Game.World;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="StellarCoreStateHandler" /> (CZ_STELLAR_STATE_SEND, op153).</summary>
public interface IStellarCoreStateService
{
    ValueTask<StellarCoreStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId, int sort,
        int value, CancellationToken cancellationToken);
}

public sealed class StellarCoreStateService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<StellarCoreStateService> logger) : IStellarCoreStateService
{
    public async ValueTask<StellarCoreStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int sort, int value, CancellationToken cancellationToken)
    {
        var context = new StellarCoreStateResolver.Context(state.StellarCoreIndex, state.StellarCoreWardrobe);
        var result = StellarCoreStateResolver.Resolve(sort, value, in context);

        switch (result.Kind)
        {
            case StellarCoreStateResolver.ResultKind.NoReply:
                return new StellarCoreStateResult(StellarCoreStateOutcome.NoReply);

            case StellarCoreStateResolver.ResultKind.Disconnect:
                return new StellarCoreStateResult(StellarCoreStateOutcome.Disconnect);

            case StellarCoreStateResolver.ResultKind.Select:
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId, result.NewCoreIndex));
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 0);

            case StellarCoreStateResolver.ResultKind.Equip:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, result.NewCoreNumber, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Equip));
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 0);
            }

            case StellarCoreStateResolver.ResultKind.Remove:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, 0, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Remove));
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 0);
            }

            case StellarCoreStateResolver.ResultKind.ReturnToInventoryMismatch:
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 1);

            case StellarCoreStateResolver.ResultKind.ReturnToInventorySuccess:
                return await GrantCoreToInventoryAsync(zone, state, characterId, result, cancellationToken);

            default:
                return new StellarCoreStateResult(StellarCoreStateOutcome.NoReply);
        }
    }

    private async ValueTask<StellarCoreStateResult> GrantCoreToInventoryAsync(Zone zone, PlayerRuntimeState state,
        int characterId, StellarCoreStateResolver.Result result, CancellationToken cancellationToken)
    {
        if (!worldData.ItemsById.TryGetValue(result.GrantedItemId, out _))
            return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 2);

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
            return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 2);

        var newStack = new ItemStack(result.GrantedItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer =
            state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        await characters.ReplaceContainerAsync(characterId, destination.Container, ToTvps(projectedContainer),
            cancellationToken);

        zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId, result.NewCoreIndex,
            WardrobeSlotCleared: result.ClearedSlot));

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped stellar-core-return-to-inventory mirror for character {CharacterId}",
                zone.MapId, characterId);

        return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, ResultCode: 0, Page: destination.Container,
            PosX: 0, PosY: 0, ItemIndex: result.GrantedItemId);
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

public enum StellarCoreStateOutcome
{
    NoReply,
    Disconnect,
    Reply
}

public readonly record struct StellarCoreStateResult(
    StellarCoreStateOutcome Outcome,
    int ResultCode = 0,
    int Page = -1,
    int PosX = -1,
    int PosY = -1,
    int ItemIndex = -1);
