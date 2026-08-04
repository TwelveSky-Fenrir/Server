using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Runes;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class RuneSocketService(
    IRuneRepository runes,
    IEventLogQueue eventLogQueue,
    WorldDataCache worldData,
    ILogger<RuneSocketService> logger)
    : IRuneSocketService
{
    private const short RuneInsertEventCode = 157;

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

        var projectedRunes = state.RuneSystem.SetItem(packet.RuneIndex, packet.ItemIndex);
        var projectedRuneStats = state.RuneSystemStat.SetItem(packet.RuneIndex, packedStat);

        await runes.PersistRunesAsync(characterId, ToRuneTvps(projectedRunes, projectedRuneStats),
            (byte)packet.Page, ToTvps(projectedContainer), cancellationToken);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneInsertEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, sourceStack.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Serial={sourceStack.Serial};ClientItemIndex={packet.ItemIndex}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-insert audit row for character {CharacterId}",
                characterId);

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, packet.ItemIndex, packedStat),
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
        var resolved = RuneSocketResolver.ResolveRemove(packet.RuneIndex, state.RuneSystem,
            true);
        if (resolved.Outcome == RuneSocketResolver.RemoveOutcome.Rejected ||
            !worldData.ItemsById.ContainsKey(resolved.ItemId))
            return new RuneRemoveResult(RuneSocketOutcome.Rejected, 0, 0, 0);

        var destination = InventoryFreeSlotFinder.Find(state.Inventory, worldData, resolved.ItemId,
            state.InventoryDate, GameDate.Today());
        if (destination is not { } placement)
            return new RuneRemoveResult(RuneSocketOutcome.InventoryFull, 0, 0, 0);

        var container = placement.Container;
        var slot = placement.Slot;
        var packedStat = state.RuneSystemStat[packet.RuneIndex];
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packedStat);
        var newStack = new ItemStack(resolved.ItemId, 0, enchant, combine, refine, socket, 0, 0, 0, 0, 0,
            placement.X, placement.Y);
        var projectedContainer = state.Inventory.GetContainer(container).SetItem(slot, newStack);

        var projectedRunes = state.RuneSystem.SetItem(packet.RuneIndex, 0);
        var projectedRuneStats = state.RuneSystemStat.SetItem(packet.RuneIndex, 0);

        await runes.PersistRunesAsync(characterId, ToRuneTvps(projectedRunes, projectedRuneStats), container,
            ToTvps(projectedContainer), cancellationToken);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneRemoveEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, resolved.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Container={container};Slot={slot}", DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-remove audit row for character {CharacterId}",
                characterId);

        if (!await zone.PostRuneSocketCommandAndWaitAsync(
                new RuneSocketZoneCommand(characterId, packet.RuneIndex, null, null), cancellationToken))
            logger.LogError(
                "Zone {MapId} rune-socket inbox full: dropped remove mirror for character {CharacterId}",
                zone.MapId, characterId);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-remove inventory mirror for character {CharacterId}",
                zone.MapId, characterId);

        return new RuneRemoveResult(RuneSocketOutcome.Applied, container, slot, resolved.ItemId, newStack);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    private static List<CharacterRuneSocketTvp> ToRuneTvps(ImmutableArray<int> runeSystem,
        ImmutableArray<int> runeSystemStat)
    {
        var list = new List<CharacterRuneSocketTvp>(runeSystem.Length);
        for (var i = 0; i < runeSystem.Length; i++)
            if (runeSystem[i] != 0)
                list.Add(new CharacterRuneSocketTvp((byte)i, runeSystem[i], runeSystemStat[i]));
        return list;
    }
}
