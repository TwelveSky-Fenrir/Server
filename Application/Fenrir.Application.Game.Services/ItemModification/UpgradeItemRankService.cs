using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op27, CZ_HIGH_ITEM_SEND -- extracted from <see cref="UpgradeItemRankHandler" />, see
///     that handler's remarks.
/// </summary>
public sealed class UpgradeItemRankService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogQueue eventLogQueue,
    ILogger<UpgradeItemRankService> logger)
    : IUpgradeItemRankService
{
    /// <summary>
    ///     game.EventLog.EventCode for an upgrade-rank attempt -- the wire opcode (op27) itself, same
    ///     "app-owned numbering scheme, caller-interpreted alongside Category" posture as every other
    ///     EventCode in this codebase.
    /// </summary>
    private const short UpgradeItemRankEventCode = 27;

    /// <summary>game.EventLog.Outcome for this EventCode: 0 success, 1 failed.</summary>
    private const byte SuccessOutcome = 0;

    private const byte FailedOutcome = 1;

    public async ValueTask<UpgradeItemRankResult> UpgradeAsync(UpgradeItemRankRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2))
            return new UpgradeItemRankResult(UpgradeItemRankOutcome.Rejected, false, 0, [0, 0, 0, 0, 0, 0]);

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
            return new UpgradeItemRankResult(UpgradeItemRankOutcome.Rejected, false, 0, [0, 0, 0, 0, 0, 0]);

        var luck = state.Stats?.Luck ?? 0;

        var resolved = RankChangeResolver.ResolveUpgrade(targetDefinition, target, materialDefinition.Item, luck, 0,
            worldData.ItemsById.Values, SystemRandomSource.Instance);

        if (resolved.Outcome == RankChangeResolver.RankChangeOutcome.Rejected)
            return new UpgradeItemRankResult(UpgradeItemRankOutcome.Rejected, false, 0, [0, 0, 0, 0, 0, 0]);

        if (resolved.Outcome == RankChangeResolver.RankChangeOutcome.NoCandidate)
            return new UpgradeItemRankResult(UpgradeItemRankOutcome.NoCandidate, false, resolved.Cost,
                [0, 0, 0, 0, 0, 0]);

        var succeeded = resolved.Outcome == RankChangeResolver.RankChangeOutcome.Success;

        var newTargetStack = succeeded
            ? target with
            {
                ItemId = resolved.ResultItemId, Enchant = (byte)resolved.NewEnchant, Combine = (byte)resolved.NewCombine
            }
            : target;

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newTargetStack);
            projectedMaterialContainer = ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2,
                newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                    ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -resolved.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} upgrade-item-rank AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new UpgradeItemRankResult(UpgradeItemRankOutcome.Rejected, false, 0, [0, 0, 0, 0, 0, 0]);
        }

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(UpgradeItemRankEventCode, (byte)EventLogCategory.Enchant,
                null, characterId, null, null, null, -(long)resolved.Cost, null, target.ItemId, target.Quantity,
                succeeded ? SuccessOutcome : FailedOutcome,
                $"Serial={target.Serial};Material={material.ItemId};ResultItemId={(succeeded ? resolved.ResultItemId : target.ItemId)}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped upgrade-item-rank audit row for character {CharacterId}",
                characterId);

        var value = succeeded
            ? new[]
            {
                resolved.ResultItemId, 0, 0, target.Quantity,
                ItemValueCodec.Encode((byte)resolved.NewEnchant, (byte)resolved.NewCombine, target.Refine,
                    target.Socket),
                target.Serial
            }
            : [0, 0, 0, 0, 0, 0];

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped upgrade-item-rank mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new UpgradeItemRankResult(UpgradeItemRankOutcome.Applied, succeeded, resolved.Cost, value);
    }

    private static bool IsValidInventorySlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
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
