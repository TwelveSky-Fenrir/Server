using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class CostumeStateService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<CostumeStateService> logger) : ICostumeStateService
{
    private const short CostumeReturnEventCode = 1;

    public async ValueTask<CostumeStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int value, CancellationToken cancellationToken)
    {
        var context = new CostumeStateResolver.Context(state.CostumeIndex, state.CostumeWardrobe);
        var result = CostumeStateResolver.Resolve(sort, value, in context);

        switch (result.Kind)
        {
            case CostumeStateResolver.ResultKind.NoReply:
                return new CostumeStateResult(CostumeStateOutcome.NoReply);

            case CostumeStateResolver.ResultKind.Disconnect:
                return new CostumeStateResult(CostumeStateOutcome.Disconnect);

            case CostumeStateResolver.ResultKind.Select:
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex));
                return new CostumeStateResult(CostumeStateOutcome.Reply);

            case CostumeStateResolver.ResultKind.Equip:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
                    result.NewCostumeNumber, Life: maxLife, Mana: maxMana,
                    Broadcast: CostumeBroadcastKind.Equip));
                logger.LogInformation("Character {CharacterId} equipped costume {CostumeNumber} at slot {CostumeIndex}",
                    characterId, result.NewCostumeNumber, result.NewCostumeIndex);
                return new CostumeStateResult(CostumeStateOutcome.Reply);
            }

            case CostumeStateResolver.ResultKind.Remove:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
                    0, Life: maxLife, Mana: maxMana, Broadcast: CostumeBroadcastKind.Remove));
                logger.LogInformation("Character {CharacterId} removed costume at slot {CostumeIndex}",
                    characterId, result.NewCostumeIndex);
                return new CostumeStateResult(CostumeStateOutcome.Reply);
            }

            case CostumeStateResolver.ResultKind.ReturnToInventoryMismatch:
                return new CostumeStateResult(CostumeStateOutcome.Reply, 1);

            case CostumeStateResolver.ResultKind.ReturnToInventorySuccess:
                return await GrantCostumeToInventoryAsync(zone, state, characterId, accountId, result,
                    cancellationToken);

            default:
                return new CostumeStateResult(CostumeStateOutcome.NoReply);
        }
    }

    private async ValueTask<CostumeStateResult> GrantCostumeToInventoryAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, CostumeStateResolver.Result result, CancellationToken cancellationToken)
    {
        if (!worldData.ItemsById.TryGetValue(result.GrantedItemId, out _))
        {
            logger.LogWarning(
                "Costume return-to-inventory denied for character {CharacterId}: unknown item {ItemId}",
                characterId, result.GrantedItemId);
            return new CostumeStateResult(CostumeStateOutcome.Reply, 2);
        }

        var freeSlot = FindFreeSlot(state.Inventory);
        if (freeSlot is not { } destination)
        {
            logger.LogInformation(
                "Costume return-to-inventory denied for character {CharacterId}: inventory full", characterId);
            return new CostumeStateResult(CostumeStateOutcome.Reply, 2);
        }

        var newStack = new ItemStack(result.GrantedItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var projectedContainer =
            state.Inventory.GetContainer(destination.Container).SetItem(destination.Slot, newStack);

        await characters.ReplaceContainerAsync(characterId, destination.Container, ToTvps(projectedContainer),
            cancellationToken);

        await eventLog.LogAsync(CostumeReturnEventCode, EventLogCategory.CosmeticDelete, accountId, characterId,
            null, null, null, null, null, result.GrantedItemId, 1, 1, $"ClearedWardrobeSlot={result.ClearedSlot}",
            cancellationToken);

        zone.PostCostumeCommand(new CostumeZoneCommand(characterId, result.NewCostumeIndex,
            WardrobeSlotCleared: result.ClearedSlot));

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(destination.Container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped costume-return-to-inventory mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} returned costume {ItemId} to inventory container {Container} slot {Slot}",
            characterId, result.GrantedItemId, destination.Container, destination.Slot);

        return new CostumeStateResult(CostumeStateOutcome.Reply, 0, destination.Container,
            0, 0, result.GrantedItemId);
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
