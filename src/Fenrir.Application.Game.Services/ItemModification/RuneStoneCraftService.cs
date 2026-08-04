using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class RuneStoneCraftService(
    ICharacterRepository characters,
    IEventLogQueue eventLogQueue,
    ILogger<RuneStoneCraftService> logger)
    : IRuneStoneCraftService
{
    private const short EventCode = 3000;

    public async ValueTask<RuneStoneCraftResult> CraftAsync(
        int sourcePage, int sourceSlot,
        int destinationPage, int destinationSlot,
        int statSlotSelector,
        bool secondInventoryPageAccessible,
        Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var sourceStack = IsValidInventorySlot(sourcePage, sourceSlot)
            ? state.Inventory.GetSlot((byte)sourcePage, (byte)sourceSlot)
            : null;
        var destinationStack = IsValidInventorySlot(destinationPage, destinationSlot)
            ? state.Inventory.GetSlot((byte)destinationPage, (byte)destinationSlot)
            : null;

        var destinationPackedStat = ItemValueCodec.Encode(destinationStack?.Enchant ?? 0,
            destinationStack?.Combine ?? 0, destinationStack?.Refine ?? 0, destinationStack?.Socket ?? 0);
        var request = new RuneStoneCraftRequest(
            sourcePage, sourceSlot, sourceStack?.ItemId ?? 0, sourceStack?.Quantity ?? 0,
            destinationPage, destinationSlot, destinationStack?.ItemId ?? 0, destinationPackedStat,
            statSlotSelector, secondInventoryPageAccessible);

        var resolved = RuneStoneCraftResolver.Resolve(request, SystemRandomSource.Instance);

        if (resolved.Outcome != RuneStoneCraftOutcome.Applied)
            return resolved;

        var source = sourceStack!.Value;
        var destination = destinationStack!.Value;
        var destinationItemId = destination.ItemId;

        var consumed = RuneStoneCraftResolver.ConsumeOneUnit(source);
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(resolved.NewPackedStat);
        var updatedDestination = destination with
        {
            Enchant = enchant, Combine = combine, Refine = refine, Socket = socket
        };

        var sourceContainer = state.Inventory.GetContainer((byte)sourcePage);
        var projectedSource = consumed is { } remaining
            ? sourceContainer.SetItem((byte)sourceSlot, remaining)
            : sourceContainer.Remove((byte)sourceSlot);

        ImmutableArray<InventoryContainerSnapshot> containers;
        if (sourcePage == destinationPage)
        {
            projectedSource = projectedSource.SetItem((byte)destinationSlot, updatedDestination);
            await characters.ReplaceContainerAsync(characterId, (byte)sourcePage, ToTvps(projectedSource),
                cancellationToken);
            containers = [new InventoryContainerSnapshot((byte)sourcePage, projectedSource)];
        }
        else
        {
            var projectedDestination = state.Inventory.GetContainer((byte)destinationPage)
                .SetItem((byte)destinationSlot, updatedDestination);
            await characters.ReplaceTwoContainersAsync(characterId, (byte)sourcePage, ToTvps(projectedSource),
                (byte)destinationPage, ToTvps(projectedDestination), cancellationToken);
            containers =
            [
                new InventoryContainerSnapshot((byte)sourcePage, projectedSource),
                new InventoryContainerSnapshot((byte)destinationPage, projectedDestination)
            ];
        }

        LogCraftAttempt(characterId, source.ItemId, destinationItemId, destinationPackedStat, resolved);

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken, Timeout.InfiniteTimeSpan))
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-stone-craft mirror for character {CharacterId}",
                zone.MapId, characterId);
            return RuneStoneCraftResult.Disconnect;
        }

        return resolved;
    }

    private void LogCraftAttempt(int characterId, int sourceItemId, int destinationItemId, int packedStatBefore,
        RuneStoneCraftResult resolved)
    {
        var actionLabel = RuneStoneCraftCatalog.GetLogActionLabel(sourceItemId);
        var (beforeStr, beforeDex, beforeVit, beforeInt) = RuneStoneStatCodec.Decode(packedStatBefore);
        var (afterStr, afterDex, afterVit, afterInt) = RuneStoneStatCodec.Decode(resolved.NewPackedStat);

        var payload =
            $"Action={actionLabel};Source={sourceItemId};Destination={destinationItemId};Slot={resolved.LogSlotIndicator};" +
            $"Before=STR{beforeStr}/DEX{beforeDex}/VIT{beforeVit}/INT{beforeInt};" +
            $"After=STR{afterStr}/DEX{afterDex}/VIT{afterVit}/INT{afterInt}";

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EventCode, (byte)EventLogCategory.Enchant, null, characterId,
                null, null, null, null, null, sourceItemId, 1, (byte)resolved.ResultCode, payload,
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-stone-craft audit row for character {CharacterId}",
                characterId);
    }

    private static bool IsValidInventorySlot(int page, int slot)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
