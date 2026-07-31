using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class CombineItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<CombineItemService> logger)
    : ICombineItemService
{
    public async ValueTask<CombineItemResult> CombineAsync(CombineItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2))
        {
            logger.LogDebug(
                "Character {CharacterId} combine-item rejected: invalid slot(s) ({Page1}:{Index1} / {Page2}:{Index2})",
                characterId, page1, index1, page2, index2);
            return new CombineItemResult(CombineItemOutcome.Rejected, 0, 0);
        }

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(page2, state.InventoryDate, today))
        {
            logger.LogDebug(
                "Character {CharacterId} combine-item rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new CombineItemResult(CombineItemOutcome.Rejected, 0, 0);
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            logger.LogDebug(
                "Character {CharacterId} combine-item rejected: target or material slot empty/unresolvable",
                characterId);
            return new CombineItemResult(CombineItemOutcome.Rejected, 0, 0);
        }

        var luck = state.Stats?.Luck ?? 0;

        var resolved = CombineResolver.Resolve(targetDefinition.Item, target, materialDefinition.Item, material,
            luck, 0, SystemRandomSource.Instance);

        if (resolved.IsRejected)
        {
            logger.LogInformation(
                "Character {CharacterId} combine-item rejected by resolver (target {TargetItemId}, material {MaterialItemId})",
                characterId, target.ItemId, material.ItemId);
            return new CombineItemResult(CombineItemOutcome.Rejected, 0, 0);
        }

        var newTargetStack = target with { Combine = (byte)resolved.NewCombine };

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = resolved.MaterialConsumed
            ? remainingMaterialQuantity > 0 ? material with { Quantity = remainingMaterialQuantity } : (ItemStack?)null
            : material;

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
                "Character {CharacterId} combine-item AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new CombineItemResult(CombineItemOutcome.Rejected, 0, 0);
        }

        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped combine mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} combine-item applied: target {TargetItemId} now Combine={NewCombine}, cost {Cost}, resultCode {ResultCode}",
            characterId, target.ItemId, resolved.NewCombine, resolved.Cost, resolved.ResultCode);

        return new CombineItemResult(CombineItemOutcome.Applied, resolved.ResultCode, resolved.Cost);
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
