using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op93, CZ_SKY_UP_ITEM_SEND -- extracted from <see cref="SkyUpgradeItemHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class SkyUpgradeItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogQueue eventLogQueue,
    ILogger<SkyUpgradeItemService> logger)
    : ISkyUpgradeItemService
{
    /// <summary>
    ///     game.EventLog.EventCode for a sky-upgrade attempt -- the wire opcode (op93) itself, same
    ///     "app-owned numbering scheme, caller-interpreted alongside Category" posture as every other
    ///     EventCode in this codebase.
    /// </summary>
    private const short SkyUpgradeItemEventCode = 93;

    /// <summary>game.EventLog.Outcome for this EventCode: 0 success, 1 failed.</summary>
    private const byte SuccessOutcome = 0;

    private const byte FailedOutcome = 1;

    public async ValueTask<SkyUpgradeItemResult> UpgradeAsync(SkyUpgradeItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1) ||
            page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
            return new SkyUpgradeItemResult(SkyUpgradeItemOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
            return new SkyUpgradeItemResult(SkyUpgradeItemOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

        var resolved = SkyUpgradeResolver.Resolve(targetDefinition.Item, target.Enchant, material.ItemId,
            SystemRandomSource.Instance);

        if (resolved.Outcome == SkyUpgradeResolver.Outcome.Rejected)
            return new SkyUpgradeItemResult(SkyUpgradeItemOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        var newTargetStack = resolved.Succeeded
            ? target with { ItemId = resolved.NewItemId, Enchant = resolved.NewEnchant }
            : target;

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            projectedMaterialContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2, newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -SkyUpgradeResolver.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -SkyUpgradeResolver.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} sky-upgrade AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new SkyUpgradeItemResult(SkyUpgradeItemOutcome.Rejected, false, [0, 0, 0, 0, 0, 0]);
        }

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(SkyUpgradeItemEventCode, (byte)EventLogCategory.Enchant,
                null, characterId, null, null, null, -(long)SkyUpgradeResolver.Cost, null, target.ItemId,
                target.Quantity, resolved.Succeeded ? SuccessOutcome : FailedOutcome,
                $"Serial={target.Serial};Material={material.ItemId};NewItemId={(resolved.Succeeded ? resolved.NewItemId : target.ItemId)}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped sky-upgrade audit row for character {CharacterId}",
                characterId);

        var packedValue = ItemValueCodec.Encode(newTargetStack.Enchant, newTargetStack.Combine,
            newTargetStack.Refine, newTargetStack.Socket);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped sky-upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new SkyUpgradeItemResult(SkyUpgradeItemOutcome.Applied, resolved.Succeeded,
            [newTargetStack.ItemId, index1 % 8, index1 / 8, target.Quantity, packedValue, target.Serial]);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? value)
    {
        return value is { } v ? current.SetItem(slot, v) : current.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
