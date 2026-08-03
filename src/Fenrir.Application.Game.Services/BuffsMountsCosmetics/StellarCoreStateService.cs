using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.StellarCores;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

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
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply);

            case StellarCoreStateResolver.ResultKind.Equip:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, result.NewCoreNumber, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Equip));
                logger.LogInformation("Character {CharacterId} equipped stellar core {CoreNumber} at slot {CoreIndex}",
                    characterId, result.NewCoreNumber, result.NewCoreIndex);
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply);
            }

            case StellarCoreStateResolver.ResultKind.Remove:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostStellarCoreCommand(new StellarCoreZoneCommand(characterId,
                    result.NewCoreIndex, 0, Life: maxLife, Mana: maxMana,
                    Broadcast: StellarCoreBroadcastKind.Remove));
                logger.LogInformation("Character {CharacterId} removed stellar core at slot {CoreIndex}",
                    characterId, result.NewCoreIndex);
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply);
            }

            case StellarCoreStateResolver.ResultKind.ReturnToInventoryMismatch:
                return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, 1);

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
        {
            logger.LogWarning(
                "Stellar-core return-to-inventory denied for character {CharacterId}: unknown item {ItemId}",
                characterId, result.GrantedItemId);
            return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, 2);
        }

        var freeSlot = InventoryFreeSlotFinder.Find(state.Inventory, worldData, result.GrantedItemId,
            state.InventoryDate, GameDate.Today());
        if (freeSlot is not { } destination)
        {
            logger.LogInformation(
                "Stellar-core return-to-inventory denied for character {CharacterId}: inventory full", characterId);
            return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, 2);
        }

        var newStack = new ItemStack(result.GrantedItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, destination.X,
            destination.Y);
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

        logger.LogInformation(
            "Character {CharacterId} returned stellar core {ItemId} to inventory container {Container} slot {Slot}",
            characterId, result.GrantedItemId, destination.Container, destination.Slot);

        return new StellarCoreStateResult(StellarCoreStateOutcome.Reply, 0, destination.Container,
            destination.Slot, destination.GridIndex, result.GrantedItemId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
