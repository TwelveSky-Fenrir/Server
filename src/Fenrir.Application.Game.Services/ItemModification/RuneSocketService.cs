using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
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

        try
        {
            await runes.PersistRunesAsync(characterId, ToRuneTvps(projectedRunes, projectedRuneStats),
                (byte)packet.Page, ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortInsertAfterUncertainPersistence(state, characterId, "rune insert", ex);
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.Page, projectedContainer));
        var inventoryResult = await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortInsertAfterDurableMutation(state, characterId, "rune-insert inventory", inventoryResult);

        var runeResult = await zone.PostRuneSocketCommandAndWaitForResultAsync(
            new RuneSocketZoneCommand(characterId, packet.RuneIndex, packet.ItemIndex, packedStat), cancellationToken);
        if (runeResult.Kind != ZoneCommandResultKind.Applied)
            return AbortInsertAfterDurableMutation(state, characterId, "rune-insert socket", runeResult);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneInsertEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, sourceStack.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Serial={sourceStack.Serial};ClientItemIndex={packet.ItemIndex}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-insert audit row for character {CharacterId}",
                characterId);

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

        try
        {
            await runes.PersistRunesAsync(characterId, ToRuneTvps(projectedRunes, projectedRuneStats), container,
                ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortRemoveAfterUncertainPersistence(state, characterId, "rune remove", ex);
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projectedContainer));
        var inventoryResult = await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortRemoveAfterDurableMutation(state, characterId, "rune-remove inventory", inventoryResult);

        var runeResult = await zone.PostRuneSocketCommandAndWaitForResultAsync(
            new RuneSocketZoneCommand(characterId, packet.RuneIndex, null, null), cancellationToken);
        if (runeResult.Kind != ZoneCommandResultKind.Applied)
            return AbortRemoveAfterDurableMutation(state, characterId, "rune-remove socket", runeResult);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(RuneRemoveEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, null, null, resolved.ItemId, 1, 0,
                $"RuneIndex={packet.RuneIndex};Container={container};Slot={slot}", DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-remove audit row for character {CharacterId}",
                characterId);

        return new RuneRemoveResult(RuneSocketOutcome.Applied, container, slot, resolved.ItemId, newStack);
    }

    private RuneInsertResult AbortInsertAfterUncertainPersistence(PlayerRuntimeState state, int characterId,
        string mutation, Exception exception)
    {
        AbortWithoutSuccess(state, characterId, mutation, exception);
        return new RuneInsertResult(RuneSocketOutcome.Disconnected);
    }

    private RuneRemoveResult AbortRemoveAfterUncertainPersistence(PlayerRuntimeState state, int characterId,
        string mutation, Exception exception)
    {
        AbortWithoutSuccess(state, characterId, mutation, exception);
        return new RuneRemoveResult(RuneSocketOutcome.Disconnected, 0, 0, 0);
    }

    private RuneInsertResult AbortInsertAfterDurableMutation(PlayerRuntimeState state, int characterId,
        string mutation, ZoneCommandResult result)
    {
        AbortWithoutSuccess(state, characterId, mutation, result);
        return new RuneInsertResult(RuneSocketOutcome.Disconnected);
    }

    private RuneRemoveResult AbortRemoveAfterDurableMutation(PlayerRuntimeState state, int characterId,
        string mutation, ZoneCommandResult result)
    {
        AbortWithoutSuccess(state, characterId, mutation, result);
        return new RuneRemoveResult(RuneSocketOutcome.Disconnected, 0, 0, 0);
    }

    private void AbortWithoutSuccess(PlayerRuntimeState state, int characterId, string mutation, Exception exception)
    {
        logger.LogError(exception,
            "Character {CharacterId} {Mutation} persistence failed after submission; durability is uncertain, disconnecting without success response",
            characterId, mutation);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
    }

    private void AbortWithoutSuccess(PlayerRuntimeState state, int characterId, string mutation,
        ZoneCommandResult result)
    {
        logger.LogError(
            "Character {CharacterId} persisted rune-socket mutation but {Mutation} actor mutation was not acknowledged as applied ({Kind}: {Cause}); disconnecting without success response",
            characterId, mutation, result.Kind, result.Cause);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
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
