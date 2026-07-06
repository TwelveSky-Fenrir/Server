using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Runes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for CZ_RUNE_SYSTEM_SEND (op157) -- extracted from <see cref="RuneSocketHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class RuneSocketService(
    ICharacterRepository characters,
    IEventLogQueue eventLogQueue,
    ILogger<RuneSocketService> logger)
    : IRuneSocketService
{
    /// <summary>
    ///     game.EventLog.EventCode for a rune-insert attempt -- the wire opcode (op157) itself. See
    ///     <see cref="RuneRemoveEventCode" /> for why remove gets its own, distinct code within the same
    ///     opcode/Category pair.
    /// </summary>
    private const short RuneInsertEventCode = 157;

    /// <summary>
    ///     game.EventLog.EventCode for a rune-remove attempt -- op157's sort=1 sub-action. Insert and remove
    ///     are opposite, independently interesting operations for audit purposes, so each gets its own
    ///     EventCode rather than sharing op157's number and relying on Outcome/Payload alone to disambiguate --
    ///     same "app-owned numbering scheme, caller-interpreted alongside Category" posture as every other
    ///     EventCode in this codebase.
    /// </summary>
    private const short RuneRemoveEventCode = 158;

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

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneInsertEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, sourceStack.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Serial={sourceStack.Serial};ClientItemIndex={packet.ItemIndex}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-insert audit row for character {CharacterId}",
                characterId);

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

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneRemoveEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, resolved.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Container={container};Slot={slot}", DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-remove audit row for character {CharacterId}",
                characterId);

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
