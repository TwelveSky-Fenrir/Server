using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Runes;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.ItemModification.Services;

public enum RuneSocketOutcome
{
    Rejected,
    InventoryFull,
    Applied
}

public readonly record struct RuneInsertResult(RuneSocketOutcome Outcome);

public readonly record struct RuneRemoveResult(RuneSocketOutcome Outcome, byte Page, byte Index, int ItemIndex);

public interface IRuneSocketService
{
    ValueTask<RuneInsertResult> InsertAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    ValueTask<RuneRemoveResult> RemoveAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}

/// <summary>
///     Business logic for CZ_RUNE_SYSTEM_SEND (op157) -- extracted from <see cref="RuneSocketHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class RuneSocketService(
    ICharacterRepository characters,
    ILogger<RuneSocketService> logger)
    : IRuneSocketService
{
    public async ValueTask<RuneInsertResult> InsertAsync(RuneSocketRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (packet.Page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page, packet.Index))
            return new RuneInsertResult(RuneSocketOutcome.Rejected);

        var source = state.Inventory.GetSlot((byte)packet.Page, (byte)packet.Index);
        if (source is not { } sourceStack)
            return new RuneInsertResult(RuneSocketOutcome.Rejected);

        var resolved = RuneSocketResolver.ResolveInsert(packet.RuneIndex, sourceStack.ItemId, state.RuneSystem);
        if (!resolved.Succeeded)
            return new RuneInsertResult(RuneSocketOutcome.Rejected);

        var packedStat = ItemValueCodec.Encode(sourceStack.Enchant, sourceStack.Combine, sourceStack.Refine,
            sourceStack.Socket);
        var projectedContainer = state.Inventory.GetContainer((byte)packet.Page).Remove((byte)packet.Index);

        await characters.ReplaceContainerAsync(characterId, (byte)packet.Page, ToTvps(projectedContainer),
            cancellationToken);

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, packet.ItemIndex, packedStat, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} rune-socket inbox full: dropped insert mirror for character {CharacterId}",
                zone.MapId, characterId);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.Page, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-insert inventory mirror for character {CharacterId}",
                zone.MapId, characterId);

        return new RuneInsertResult(RuneSocketOutcome.Applied);
    }

    public async ValueTask<RuneRemoveResult> RemoveAsync(RuneSocketRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var destination = FindFreeSlot(state.Inventory);

        var resolved = RuneSocketResolver.ResolveRemove(packet.RuneIndex, state.RuneSystem, destination is not null);
        switch (resolved.Outcome)
        {
            case RuneSocketResolver.RemoveOutcome.Rejected:
                return new RuneRemoveResult(RuneSocketOutcome.Rejected, 0, 0, 0);

            case RuneSocketResolver.RemoveOutcome.InventoryFull:
                return new RuneRemoveResult(RuneSocketOutcome.InventoryFull, 0, 0, 0);
        }

        var (container, slot) = destination!.Value;
        var packedStat = state.RuneSystemStat[packet.RuneIndex];
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packedStat);
        var newStack = new ItemStack(resolved.ItemId, 0, enchant, combine, refine, socket, 0, 0, 0, 0, 0);
        var projectedContainer = state.Inventory.GetContainer(container).SetItem(slot, newStack);

        await characters.ReplaceContainerAsync(characterId, container, ToTvps(projectedContainer),
            cancellationToken);

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, null, null, null), cancellationToken))
            logger.LogError(
                "Zone {MapId} rune-socket inbox full: dropped remove mirror for character {CharacterId}",
                zone.MapId, characterId);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-remove inventory mirror for character {CharacterId}",
                zone.MapId, characterId);

        return new RuneRemoveResult(RuneSocketOutcome.Applied, container, slot, resolved.ItemId);
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
